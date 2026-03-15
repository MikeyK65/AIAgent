using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIAgent.Models;

namespace AIAgent.Services;

public sealed class OllamaEmailSummarizer(HttpClient httpClient, ISettingsService settingsService) : IEmailSummarizer
{
    public async Task<string> SummarizeAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();

        if (!settings.EnableAiSummaries)
        {
            return "AI summaries are disabled. Enable them in Settings to summarize emails with your local Ollama installation.";
        }

        if (string.IsNullOrWhiteSpace(settings.OllamaBaseUrl) || string.IsNullOrWhiteSpace(settings.OllamaModel))
        {
            return "Ollama settings are incomplete. Add the base URL and model name in Settings.";
        }

        var request = new OllamaGenerateRequest
        {
            Model = settings.OllamaModel,
            Stream = false,
            Prompt = BuildPrompt(message)
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync($"{settings.OllamaBaseUrl.TrimEnd('/')}/api/generate", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return $"Ollama returned {(int)response.StatusCode}. Check that the local server is running and the model is available.";
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.Response)
                ? "Ollama returned an empty summary."
                : payload.Response.Trim();
        }
        catch (Exception ex)
        {
            return $"Unable to generate a summary from Ollama: {ex.Message}";
        }
    }

    public async Task<string> SummarizeAsync(IReadOnlyList<MailMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return "Select at least one email to summarize.";
        }

        if (messages.Count == 1)
        {
            return await SummarizeAsync(messages[0], cancellationToken);
        }

        var settings = settingsService.GetSettings();

        if (!settings.EnableAiSummaries)
        {
            return "AI summaries are disabled. Enable them in Settings to summarize emails with your local Ollama installation.";
        }

        if (string.IsNullOrWhiteSpace(settings.OllamaBaseUrl) || string.IsNullOrWhiteSpace(settings.OllamaModel))
        {
            return "Ollama settings are incomplete. Add the base URL and model name in Settings.";
        }

        var request = new OllamaGenerateRequest
        {
            Model = settings.OllamaModel,
            Stream = false,
            Prompt = BuildPrompt(messages)
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync($"{settings.OllamaBaseUrl.TrimEnd('/')}/api/generate", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return $"Ollama returned {(int)response.StatusCode}. Check that the local server is running and the model is available.";
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.Response)
                ? "Ollama returned an empty summary."
                : payload.Response.Trim();
        }
        catch (Exception ex)
        {
            return $"Unable to generate a summary from Ollama: {ex.Message}";
        }
    }

    private static string BuildPrompt(MailMessage message)
    {
        return $"Summarise this email in 3 bullet points and list any action items at the end.\n\nSubject: {message.Subject}\nFrom: {message.SenderLine}\nCategories: {message.CategoriesText}\nImportant because: {message.ImportanceReasonsText}\n\nBody:\n{message.BodyText}";
    }

    private static string BuildPrompt(IReadOnlyList<MailMessage> messages)
    {
        var content = string.Join(
            "\n\n---\n\n",
            messages.Select((message, index) =>
                $"Email {index + 1}\nSubject: {message.Subject}\nFrom: {message.SenderLine}\nReceived: {message.ReceivedText}\nCategories: {message.CategoriesText}\nImportant because: {message.ImportanceReasonsText}\nPreview: {message.Preview}\nBody:\n{message.BodyText}"));

        return $"Summarise these selected emails as a combined briefing. Provide:\n1. A short overall summary paragraph.\n2. Bullet points for the most important themes across the emails.\n3. A final 'Action items' section listing any follow-up tasks.\n4. Mention the subject when referring to a specific email.\n\nSelected emails:\n\n{content}";
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
