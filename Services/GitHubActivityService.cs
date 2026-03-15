using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIAgent.Models;

namespace AIAgent.Services;

public sealed class GitHubActivityService(HttpClient httpClient, ISettingsService settingsService) : IGitHubActivityService
{
    public async Task<IReadOnlyList<DeveloperActivityItem>> GetRecentActivityAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.GitHubActivityLink))
        {
            throw new InvalidOperationException("Add a GitHub profile or repository link in Settings before refreshing.");
        }

        var limit = Math.Clamp(settings.GitHubRecentActivityCount, 1, 50);
        var endpoint = BuildEventsEndpoint(settings.GitHubActivityLink, limit);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AIAgent", "1.0"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PriorityMail", "1.0"));

        if (!string.IsNullOrWhiteSpace(settings.GitHubAuthenticationToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GitHubAuthenticationToken.Trim());
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("GitHub authentication failed. Check the token in Settings and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub returned {(int)response.StatusCode}. Verify the configured link and try again.");
        }

        var payload = await response.Content.ReadFromJsonAsync<List<GitHubEvent>>(cancellationToken: cancellationToken) ?? [];
        return payload
            .Take(limit)
            .Select(activity => new DeveloperActivityItem
            {
                Title = BuildTitle(activity),
                Description = BuildDescription(activity),
                Context = activity.Repo?.Name ?? ExtractContextFromUrl(settings.GitHubActivityLink),
                DetailUrl = BuildDetailUrl(activity, settings.GitHubActivityLink),
                OccurredAt = activity.CreatedAt
            })
            .OrderByDescending(activity => activity.OccurredAt)
            .ToList();
    }

    private static string BuildEventsEndpoint(string configuredLink, int limit)
    {
        if (!Uri.TryCreate(configuredLink, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The GitHub link in Settings is not a valid absolute URL.");
        }

        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Use a GitHub profile, organization, or repository link in Settings.");
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("The GitHub link in Settings must point to a profile, organization, or repository.");
        }

        var endpointPath = segments[0] switch
        {
            "users" when segments.Length >= 2 => $"/users/{segments[1]}/events",
            "orgs" when segments.Length >= 2 => $"/orgs/{segments[1]}/events",
            _ when segments.Length >= 2 => $"/repos/{segments[0]}/{segments[1]}/events",
            _ => $"/users/{segments[0]}/events"
        };

        return $"https://api.github.com{endpointPath}?per_page={limit}";
    }

    private static string BuildTitle(GitHubEvent activity)
    {
        var context = activity.Repo?.Name ?? "GitHub";
        return activity.Type switch
        {
            "PushEvent" => $"Push to {context}",
            "PullRequestEvent" => $"Pull request in {context}",
            "IssuesEvent" => $"Issue update in {context}",
            "IssueCommentEvent" => $"Issue comment in {context}",
            "CreateEvent" => $"Created {activity.Payload?.RefType ?? "resource"} in {context}",
            "ReleaseEvent" => $"Release published in {context}",
            "ForkEvent" => $"Repository fork in {context}",
            "WatchEvent" => $"Repository starred in {context}",
            _ => $"{activity.Type} in {context}"
        };
    }

    private static string BuildDescription(GitHubEvent activity)
    {
        var actor = activity.Actor?.Login ?? "Unknown user";
        var repo = activity.Repo?.Name ?? "GitHub";
        var action = activity.Payload?.Action;
        var commitCount = activity.Payload?.Commits?.Count ?? 0;
        var detail = activity.Type switch
        {
            "PushEvent" when commitCount > 0 => $"{actor} pushed {commitCount} commit(s) to {repo}.",
            "PullRequestEvent" when !string.IsNullOrWhiteSpace(action) => $"{actor} {action} a pull request in {repo}.",
            "IssuesEvent" when !string.IsNullOrWhiteSpace(action) => $"{actor} {action} an issue in {repo}.",
            "IssueCommentEvent" when !string.IsNullOrWhiteSpace(action) => $"{actor} {action} an issue comment in {repo}.",
            "CreateEvent" when !string.IsNullOrWhiteSpace(activity.Payload?.RefType) => $"{actor} created a {activity.Payload.RefType} in {repo}.",
            "ReleaseEvent" => $"{actor} published a release in {repo}.",
            _ => $"{actor} triggered {activity.Type} for {repo}."
        };

        return WebUtility.HtmlDecode(detail);
    }

    private static string BuildDetailUrl(GitHubEvent activity, string configuredLink)
    {
        if (!string.IsNullOrWhiteSpace(activity.Repo?.Name))
        {
            return $"https://github.com/{activity.Repo.Name}";
        }

        return configuredLink;
    }

    private static string ExtractContextFromUrl(string configuredLink)
    {
        return Uri.TryCreate(configuredLink, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath.Trim('/').Replace('/', ' ')
            : "GitHub";
    }

    private sealed class GitHubEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("actor")]
        public GitHubActor? Actor { get; init; }

        [JsonPropertyName("repo")]
        public GitHubRepo? Repo { get; init; }

        [JsonPropertyName("payload")]
        public GitHubPayload? Payload { get; init; }
    }

    private sealed class GitHubActor
    {
        [JsonPropertyName("login")]
        public string? Login { get; init; }
    }

    private sealed class GitHubRepo
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class GitHubPayload
    {
        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("ref_type")]
        public string? RefType { get; init; }

        [JsonPropertyName("commits")]
        public List<GitHubCommit>? Commits { get; init; }
    }

    private sealed class GitHubCommit
    {
    }
}
