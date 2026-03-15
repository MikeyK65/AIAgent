using AIAgent.Models;

namespace AIAgent.Services;

public interface IGitHubActivityService
{
    Task<IReadOnlyList<DeveloperActivityItem>> GetRecentActivityAsync(CancellationToken cancellationToken = default);
}
