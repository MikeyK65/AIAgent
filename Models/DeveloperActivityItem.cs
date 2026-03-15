namespace AIAgent.Models;

public sealed class DeveloperActivityItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
    public string DetailUrl { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }

    public string OccurredText => OccurredAt == default
        ? "Time unavailable"
        : OccurredAt.LocalDateTime.ToString("g");
}
