using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using AIAgent.Models;

namespace AIAgent.Services;

public sealed class AzureDevOpsActivityService(HttpClient httpClient, ISettingsService settingsService) : IAzureDevOpsActivityService
{
    public async Task<IReadOnlyList<DeveloperActivityItem>> GetRecentActivityAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AzureDevOpsActivityLink))
        {
            throw new InvalidOperationException("Add an Azure DevOps organization or project link in Settings before refreshing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AzureDevOpsAuthenticationToken))
        {
            throw new InvalidOperationException("Add an Azure DevOps personal access token in Settings before refreshing.");
        }

        var configuration = ParseConfiguration(settings.AzureDevOpsActivityLink);
        var limit = Math.Clamp(settings.AzureDevOpsRecentActivityCount, 1, 50);

        var pullRequestsTask = GetPullRequestActivityAsync(configuration, settings.AzureDevOpsAuthenticationToken, limit, cancellationToken);
        var buildsTask = GetBuildActivityAsync(configuration, settings.AzureDevOpsAuthenticationToken, limit, cancellationToken);
        await Task.WhenAll(pullRequestsTask, buildsTask);

        return pullRequestsTask.Result
            .Concat(buildsTask.Result)
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(limit)
            .ToList();
    }

    private async Task<IReadOnlyList<DeveloperActivityItem>> GetPullRequestActivityAsync(AzureDevOpsConfiguration configuration, string token, int limit, CancellationToken cancellationToken)
    {
        var url = $"https://dev.azure.com/{configuration.Organization}/{configuration.Project}/_apis/git/pullrequests?searchCriteria.status=all&$top={limit}&api-version=7.1-preview.1";
        using var request = CreateRequest(url, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateResponse(response, "pull requests");

        var payload = await response.Content.ReadFromJsonAsync<AzureDevOpsListResponse<AzureDevOpsPullRequest>>(cancellationToken: cancellationToken);
        return payload?.Value?.Select(pullRequest => new DeveloperActivityItem
        {
            Title = pullRequest.Title ?? "Pull request",
            Description = BuildPullRequestDescription(pullRequest),
            Context = pullRequest.Repository?.Name ?? configuration.Project,
            DetailUrl = pullRequest.Links?.Web?.Href ?? configuration.SourceLink,
            OccurredAt = pullRequest.ClosedDate ?? pullRequest.CreationDate ?? pullRequest.LastMergeSourceCommit?.CommitTime ?? DateTimeOffset.MinValue
        }).ToList() ?? [];
    }

    private async Task<IReadOnlyList<DeveloperActivityItem>> GetBuildActivityAsync(AzureDevOpsConfiguration configuration, string token, int limit, CancellationToken cancellationToken)
    {
        var url = $"https://dev.azure.com/{configuration.Organization}/{configuration.Project}/_apis/build/builds?$top={limit}&queryOrder=queueTimeDescending&api-version=7.1-preview.7";
        using var request = CreateRequest(url, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateResponse(response, "builds");

        var payload = await response.Content.ReadFromJsonAsync<AzureDevOpsListResponse<AzureDevOpsBuild>>(cancellationToken: cancellationToken);
        return payload?.Value?.Select(build => new DeveloperActivityItem
        {
            Title = $"Build {build.BuildNumber}".Trim(),
            Description = BuildBuildDescription(build),
            Context = build.Definition?.Name ?? configuration.Project,
            DetailUrl = build.Links?.Web?.Href ?? configuration.SourceLink,
            OccurredAt = build.FinishTime ?? build.QueueTime ?? DateTimeOffset.MinValue
        }).ToList() ?? [];
    }

    private static HttpRequestMessage CreateRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authBytes = Encoding.ASCII.GetBytes($":{token.Trim()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        return request;
    }

    private static void ValidateResponse(HttpResponseMessage response, string activityType)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException($"Azure DevOps authentication failed while loading {activityType}. Check the personal access token in Settings.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure DevOps returned {(int)response.StatusCode} while loading {activityType}. Verify the configured link and permissions.");
        }
    }

    private static AzureDevOpsConfiguration ParseConfiguration(string configuredLink)
    {
        if (!Uri.TryCreate(configuredLink, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The Azure DevOps link in Settings is not a valid absolute URL.");
        }

        string organization;
        string project;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length < 2)
            {
                throw new InvalidOperationException("Use an Azure DevOps project link like https://dev.azure.com/{organization}/{project}.");
            }

            organization = segments[0];
            project = segments[1];
        }
        else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            organization = uri.Host.Split('.')[0];
            if (segments.Length == 0)
            {
                throw new InvalidOperationException("Use an Azure DevOps project link that includes the project path.");
            }

            project = segments[0];
        }
        else
        {
            throw new InvalidOperationException("Use an Azure DevOps project link from dev.azure.com or visualstudio.com.");
        }

        return new AzureDevOpsConfiguration(organization, project, configuredLink);
    }

    private static string BuildPullRequestDescription(AzureDevOpsPullRequest pullRequest)
    {
        var createdBy = pullRequest.CreatedBy?.DisplayName ?? "Unknown author";
        var status = pullRequest.Status ?? "unknown status";
        var repository = pullRequest.Repository?.Name ?? "repository";
        return $"{status} pull request by {createdBy} in {repository}.";
    }

    private static string BuildBuildDescription(AzureDevOpsBuild build)
    {
        var definition = build.Definition?.Name ?? "Build";
        var status = build.Status ?? "unknown status";
        var result = string.IsNullOrWhiteSpace(build.Result) ? string.Empty : $" / {build.Result}";
        return $"{definition} is {status}{result}.";
    }

    private sealed record AzureDevOpsConfiguration(string Organization, string Project, string SourceLink);

    private sealed class AzureDevOpsListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T>? Value { get; init; }
    }

    private sealed class AzureDevOpsPullRequest
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("creationDate")]
        public DateTimeOffset? CreationDate { get; init; }

        [JsonPropertyName("closedDate")]
        public DateTimeOffset? ClosedDate { get; init; }

        [JsonPropertyName("createdBy")]
        public AzureDevOpsIdentity? CreatedBy { get; init; }

        [JsonPropertyName("repository")]
        public AzureDevOpsRepository? Repository { get; init; }

        [JsonPropertyName("lastMergeSourceCommit")]
        public AzureDevOpsCommit? LastMergeSourceCommit { get; init; }

        [JsonPropertyName("_links")]
        public AzureDevOpsLinks? Links { get; init; }
    }

    private sealed class AzureDevOpsBuild
    {
        [JsonPropertyName("buildNumber")]
        public string? BuildNumber { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("result")]
        public string? Result { get; init; }

        [JsonPropertyName("queueTime")]
        public DateTimeOffset? QueueTime { get; init; }

        [JsonPropertyName("finishTime")]
        public DateTimeOffset? FinishTime { get; init; }

        [JsonPropertyName("definition")]
        public AzureDevOpsDefinition? Definition { get; init; }

        [JsonPropertyName("_links")]
        public AzureDevOpsLinks? Links { get; init; }
    }

    private sealed class AzureDevOpsIdentity
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }
    }

    private sealed class AzureDevOpsRepository
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class AzureDevOpsCommit
    {
        [JsonPropertyName("commitTime")]
        public DateTimeOffset? CommitTime { get; init; }
    }

    private sealed class AzureDevOpsDefinition
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class AzureDevOpsLinks
    {
        [JsonPropertyName("web")]
        public AzureDevOpsLink? Web { get; init; }
    }

    private sealed class AzureDevOpsLink
    {
        [JsonPropertyName("href")]
        public string? Href { get; init; }
    }
}
