namespace AIAgent.Models;

public sealed class PrioritySignal
{
    public string Type { get; init; } = string.Empty;
    public int Weight { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class PriorityScoreResult
{
    public static PriorityScoreResult Empty { get; } = new();

    public int Score { get; init; }
    public IReadOnlyList<PrioritySignal> Signals { get; init; } = Array.Empty<PrioritySignal>();
}
