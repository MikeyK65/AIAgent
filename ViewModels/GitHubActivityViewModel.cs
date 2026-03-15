using AIAgent.Models;
using AIAgent.Services;

namespace AIAgent.ViewModels;

public sealed class GitHubActivityViewModel : DeveloperActivityPageViewModelBase
{
    private readonly IGitHubActivityService gitHubActivityService;
    private readonly ISettingsService settingsService;

    public GitHubActivityViewModel(IGitHubActivityService gitHubActivityService, IActivitySummarizer activitySummarizer, ISettingsService settingsService)
        : base(activitySummarizer, "GitHub", "Refresh to load recent GitHub activity and generate a summary.")
    {
        this.gitHubActivityService = gitHubActivityService;
        this.settingsService = settingsService;
    }

    protected override string SourceName => "GitHub";

    protected override Task<IReadOnlyList<DeveloperActivityItem>> LoadActivitiesAsync()
    {
        return gitHubActivityService.GetRecentActivityAsync();
    }

    protected override string GetConfiguredLink()
    {
        return settingsService.GetSettings().GitHubActivityLink;
    }

    protected override string GetRefreshingStatusText()
    {
        return "Refreshing GitHub activity...";
    }

    protected override string GetEmptyStatusText()
    {
        return "No recent GitHub activity matched the configured link.";
    }
}
