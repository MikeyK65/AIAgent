using AIAgent.Models;
using AIAgent.Services;

namespace AIAgent.ViewModels;

public sealed class AzureDevOpsActivityViewModel : DeveloperActivityPageViewModelBase
{
    private readonly IAzureDevOpsActivityService azureDevOpsActivityService;
    private readonly ISettingsService settingsService;

    public AzureDevOpsActivityViewModel(IAzureDevOpsActivityService azureDevOpsActivityService, IActivitySummarizer activitySummarizer, ISettingsService settingsService)
        : base(activitySummarizer, "Azure DevOps", "Refresh to load recent Azure DevOps activity and generate a summary.")
    {
        this.azureDevOpsActivityService = azureDevOpsActivityService;
        this.settingsService = settingsService;
    }

    protected override string SourceName => "Azure DevOps";

    protected override Task<IReadOnlyList<DeveloperActivityItem>> LoadActivitiesAsync()
    {
        return azureDevOpsActivityService.GetRecentActivityAsync();
    }

    protected override string GetConfiguredLink()
    {
        return settingsService.GetSettings().AzureDevOpsActivityLink;
    }

    protected override string GetRefreshingStatusText()
    {
        return "Refreshing Azure DevOps activity...";
    }

    protected override string GetEmptyStatusText()
    {
        return "No recent Azure DevOps activity matched the configured project link.";
    }
}
