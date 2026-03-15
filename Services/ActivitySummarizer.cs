using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIAgent.Models;

namespace AIAgent.Services;

public sealed class ActivitySummarizer(HttpClient httpClient, ISettingsService settingsService) : IActivitySummarizer
{
    public async Task<string> SummarizeAsync(string sourceName, IReadOnlyList<DeveloperActivityItem> activities, CancellationToken cancellationToken = default)
    {
        if (activities.Count == 0)
        {
            return $"No recent {sourceName} activity was returned for the current settings.";
        }

        var settings = settingsService.GetSettings();
        if (!settings.EnableAiSummaries
            || string.IsNullOrWhiteSpace(settings.OllamaBaseUrl)
            || string.IsNullOrWhiteSpace(settings.OllamaModel))
        {
            return BuildFallbackSummary(sourceName, activities);
        }

        var request = new OllamaGenerateRequest
        {
            Model = settings.OllamaModel,
            Stream = false,
            Prompt = BuildPrompt(sourceName, activities)
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync($"{settings.OllamaBaseUrl.TrimEnd('/')}/api/generate", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return BuildFallbackSummary(sourceName, activities);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.Response)
                ? BuildFallbackSummary(sourceName, activities)
                : payload.Response.Trim();
        }
        catch
        {
            return BuildFallbackSummary(sourceName, activities);
        }
    }

    private static string BuildFallbackSummary(string sourceName, IReadOnlyList<DeveloperActivityItem> activities)
    {
        var orderedActivities = activities
            .OrderByDescending(activity => activity.OccurredAt)
            .ToList();
        var latestActivity = orderedActivities[0];
        var focusAreas = orderedActivities
            .Select(activity => activity.Context)
            .Where(context => !string.IsNullOrWhiteSpace(context))
            .GroupBy(context => context)
            .OrderByDescending(group => group.Count())
            .Take(3)
            .Select(group => $"{group.Key} ({group.Count()})")
            .ToList();
        var highlights = orderedActivities
            .Take(3)
            .Select(activity => $"• {activity.Title} — {activity.Description}")
            .ToList();

        var summary = $"{sourceName} has {activities.Count} recent activities. Latest update: {latestActivity.Title} at {latestActivity.OccurredText}.";
        if (focusAreas.Count > 0)
        {
            summary += $" Focus areas: {string.Join(", ", focusAreas)}.";
        }

        return $"{summary}\n\nRecent highlights:\n{string.Join("\n", highlights)}";
    }

    private static string BuildPrompt(string sourceName, IReadOnlyList<DeveloperActivityItem> activities)
    {
        var content = string.Join(
            "\n\n---\n\n",
            activities.Select((activity, index) =>
                $"Activity {index + 1}\nTitle: {activity.Title}\nContext: {activity.Context}\nOccurred: {activity.OccurredText}\nDescription: {activity.Description}\nLink: {activity.DetailUrl}"));

        return $"Summarize these recent {sourceName} activities. Provide:\n1. A short overview paragraph.\n2. Bullet points for the most important changes or themes.\n3. A final action items section only when follow-up is implied by the activity.\n\nRecent activity:\n\n{content}";
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; init; }
    }
}
