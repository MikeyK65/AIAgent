using AIAgent.Models;

namespace AIAgent.Services;

public interface IAzureDevOpsActivityService
{
    Task<IReadOnlyList<DeveloperActivityItem>> GetRecentActivityAsync(CancellationToken cancellationToken = default);
}
