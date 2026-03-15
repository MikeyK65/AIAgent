using System.Collections.ObjectModel;
using AIAgent.Models;
using AIAgent.Services;
using Microsoft.Maui.ApplicationModel;

namespace AIAgent.ViewModels;

public abstract class DeveloperActivityPageViewModelBase(IActivitySummarizer activitySummarizer) : BaseViewModel
{
    private string statusText = string.Empty;
    private string lastRefreshText = "Not refreshed yet";
    private string summaryText = "Refresh to generate a summary.";
    private string sourceLink = string.Empty;
    private bool hasLoaded;

    protected DeveloperActivityPageViewModelBase(IActivitySummarizer activitySummarizer, string title, string initialStatusText) : this(activitySummarizer)
    {
        Title = title;
        statusText = initialStatusText;
        RefreshCommand = new Command(async () => await RefreshAsync());
        OpenSourceCommand = new Command(async () => await OpenSourceAsync(), () => HasSourceLink);
    }

    public ObservableCollection<DeveloperActivityItem> Activities { get; } = new();
    public Command RefreshCommand { get; }
    public Command OpenSourceCommand { get; }

    public string StatusText
    {
        get => statusText;
        protected set => SetProperty(ref statusText, value);
    }

    public string LastRefreshText
    {
        get => lastRefreshText;
        protected set => SetProperty(ref lastRefreshText, value);
    }

    public string SummaryText
    {
        get => summaryText;
        protected set => SetProperty(ref summaryText, value);
    }

    public bool HasActivities => Activities.Count > 0;

    public bool HasNoActivities => !HasActivities;

    public string SourceLink
    {
        get => sourceLink;
        protected set
        {
            if (SetProperty(ref sourceLink, value))
            {
                OnPropertyChanged(nameof(HasSourceLink));
                OpenSourceCommand.ChangeCanExecute();
            }
        }
    }

    public bool HasSourceLink => !string.IsNullOrWhiteSpace(SourceLink);

    public async Task InitializeAsync()
    {
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SourceLink = GetConfiguredLink();
            StatusText = GetRefreshingStatusText();

            var activities = await LoadActivitiesAsync();
            Activities.Clear();
            foreach (var activity in activities)
            {
                Activities.Add(activity);
            }

            OnPropertyChanged(nameof(HasActivities));
            OnPropertyChanged(nameof(HasNoActivities));
            SummaryText = await activitySummarizer.SummarizeAsync(SourceName, activities);
            LastRefreshText = $"Last refreshed {DateTime.Now:t}";
            StatusText = activities.Count == 0
                ? GetEmptyStatusText()
                : GetLoadedStatusText(activities.Count);
        }
        catch (Exception ex)
        {
            Activities.Clear();
            OnPropertyChanged(nameof(HasActivities));
            OnPropertyChanged(nameof(HasNoActivities));
            SummaryText = "Unable to generate a summary until activity can be loaded.";
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected abstract string SourceName { get; }
    protected abstract Task<IReadOnlyList<DeveloperActivityItem>> LoadActivitiesAsync();
    protected abstract string GetConfiguredLink();
    protected abstract string GetRefreshingStatusText();
    protected abstract string GetEmptyStatusText();

    protected virtual string GetLoadedStatusText(int count)
    {
        return $"Loaded {count} recent {SourceName} activities.";
    }

    private async Task OpenSourceAsync()
    {
        if (!HasSourceLink)
        {
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(new Uri(SourceLink));
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to open the configured link. {ex.Message}";
        }
    }
}
