using AIAgent.Models;

namespace AIAgent.Services;

public interface IActivitySummarizer
{
    Task<string> SummarizeAsync(string sourceName, IReadOnlyList<DeveloperActivityItem> activities, CancellationToken cancellationToken = default);
}
