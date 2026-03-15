using System.Collections.ObjectModel;
using AIAgent.Models;
using AIAgent.Services;
using AIAgent.Views;

namespace AIAgent.ViewModels;

public sealed class CombinedInboxViewModel : BaseViewModel
{
    private const string SortHighestScore = "Highest score";
    private const string SortNewestFirst = "Newest first";
    private const string SortOldestFirst = "Oldest first";
    private const string SortUnreadFirst = "Unread first";
    private const string SortSenderAscending = "Sender A-Z";
    private const string SortSubjectAscending = "Subject A-Z";

    private readonly MessageAggregationService messageAggregationService;
    private readonly IEmailSummarizer emailSummarizer;
    private readonly List<MailMessage> allMessages = new();
    private readonly List<MailMessage> selectedMessages = new();
    private bool isResettingFilters;
    private bool isLoadingMore;
    private bool hasMoreMessages;
    private bool isSelectionModeEnabled;
    private string selectedAccountFilter = "All accounts";
    private string selectedCategory = "All categories";
    private string selectedSortOption = SortHighestScore;
    private bool showPinnedOnly;
    private bool showFlaggedOnly;
    private bool showRepliesOnly;
    private bool showFromMeOnly;
    private bool showFromWifeOnly;
    private string statusText = "Load the combined important inbox.";
    private string lastRefreshText = "Not refreshed yet";
    private string selectionStatusText = "Selection mode is off. Turn it on to pick emails for an AI summary.";
    private bool hasLoaded;

 public CombinedInboxViewModel(MessageAggregationService messageAggregationService, IEmailSummarizer emailSummarizer)
    {
        this.messageAggregationService = messageAggregationService;
        this.emailSummarizer = emailSummarizer;
        Title = "Important";
        RefreshCommand = new Command(async () => await RefreshAsync());
        LoadMoreCommand = new Command(async () => await LoadMoreAsync(), CanLoadMoreMessages);
        ClearFiltersCommand = new Command(ClearFilters);
        AccountFilterOptions = new ObservableCollection<string> { "All accounts", "Personal", "Shared" };
        CategoryOptions = new ObservableCollection<string> { "All categories" };
        SortOptions = new ObservableCollection<string>
        {
            SortHighestScore,
            SortNewestFirst,
            SortOldestFirst,
            SortUnreadFirst,
            SortSenderAscending,
            SortSubjectAscending
        };
    }

    public ObservableCollection<MailMessage> Messages { get; } = new();
    public ObservableCollection<string> AccountFilterOptions { get; }
    public ObservableCollection<string> CategoryOptions { get; }
    public ObservableCollection<string> SortOptions { get; }
    public Command RefreshCommand { get; }
    public Command LoadMoreCommand { get; }
    public Command ClearFiltersCommand { get; }

    public SelectionMode MessageSelectionMode => IsSelectionModeEnabled ? SelectionMode.Multiple : SelectionMode.Single;

    public bool IsSelectionModeEnabled
    {
        get => isSelectionModeEnabled;
        set
        {
            if (SetProperty(ref isSelectionModeEnabled, value))
            {
                if (!value)
                {
                    ClearSelection();
                }
                else
                {
                    UpdateSelectionState();
                }

                OnPropertyChanged(nameof(MessageSelectionMode));
                OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            }
        }
    }

    public int SelectedMessageCount => selectedMessages.Count;

    public string SelectionStatusText
    {
        get => selectionStatusText;
        private set => SetProperty(ref selectionStatusText, value);
    }

    public bool CanSummarizeSelectedMessages => IsSelectionModeEnabled && selectedMessages.Count > 0 && !IsBusy && !IsLoadingMore;

    public bool IsLoadingMore
    {
        get => isLoadingMore;
        private set
        {
            if (SetProperty(ref isLoadingMore, value))
            {
                LoadMoreCommand.ChangeCanExecute();
                OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            }
        }
    }

    public string SelectedSortOption
    {
        get => selectedSortOption;
        set
        {
            if (SetProperty(ref selectedSortOption, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool HasMoreMessages
    {
        get => hasMoreMessages;
        private set
        {
            if (SetProperty(ref hasMoreMessages, value))
            {
                LoadMoreCommand.ChangeCanExecute();
            }
        }
    }

    public string SelectedAccountFilter
    {
        get => selectedAccountFilter;
        set
        {
            if (SetProperty(ref selectedAccountFilter, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (SetProperty(ref selectedCategory, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool ShowPinnedOnly
    {
        get => showPinnedOnly;
        set
        {
            if (SetProperty(ref showPinnedOnly, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool ShowFlaggedOnly
    {
        get => showFlaggedOnly;
        set
        {
            if (SetProperty(ref showFlaggedOnly, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool ShowRepliesOnly
    {
        get => showRepliesOnly;
        set
        {
            if (SetProperty(ref showRepliesOnly, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool ShowFromMeOnly
    {
        get => showFromMeOnly;
        set
        {
            if (SetProperty(ref showFromMeOnly, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public bool ShowFromWifeOnly
    {
        get => showFromWifeOnly;
        set
        {
            if (SetProperty(ref showFromWifeOnly, value))
            {
                ApplyFiltersIfReady();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public string LastRefreshText
    {
        get => lastRefreshText;
        set => SetProperty(ref lastRefreshText, value);
    }

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
            OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            LoadMoreCommand.ChangeCanExecute();
            StatusText = "Refreshing important email...";

            var page = await messageAggregationService.RefreshMessagesAsync();
            ReplaceAllMessages(page.Messages);
            HasMoreMessages = page.HasMoreMessages;

            IReadOnlyList<string> masterCategories = [];
            try
            {
                masterCategories = await messageAggregationService.GetCategoriesAsync();
            }
            catch
            {
            }

            RebuildCategoryOptions(masterCategories);
            ApplyFilters();

            LastRefreshText = $"Last refreshed {DateTime.Now:t}";
            StatusText = $"Showing {Messages.Count} important emails across both Outlook accounts.";
        }
        catch (Exception ex)
        {
            Messages.Clear();
            HasMoreMessages = false;
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            LoadMoreCommand.ChangeCanExecute();
        }
    }

    public async Task LoadMoreAsync()
    {
        if (!CanLoadMoreMessages())
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            StatusText = "Loading more important email...";

            var page = await messageAggregationService.LoadMoreMessagesAsync();
            ReplaceAllMessages(page.Messages);
            HasMoreMessages = page.HasMoreMessages;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            HasMoreMessages = false;
            StatusText = $"Couldn't load more emails. Pull to refresh and try again. {ex.Message}";
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    public async Task OpenMessageAsync(MailMessage? message)
    {
        if (message is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(MessageDetailPage)}?messageId={message.Id}");
    }

    public void UpdateSelection(IReadOnlyList<object> currentSelection)
    {
        selectedMessages.Clear();

        if (IsSelectionModeEnabled)
        {
            selectedMessages.AddRange(currentSelection.OfType<MailMessage>());
        }

        UpdateSelectionState();
    }

    public void ClearSelection()
    {
        if (selectedMessages.Count == 0)
        {
            SelectionStatusText = IsSelectionModeEnabled
                ? "No emails selected yet. Tap emails to add them to the AI summary."
                : "Selection mode is off. Turn it on to pick emails for an AI summary.";
            OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            return;
        }

        selectedMessages.Clear();
        UpdateSelectionState();
    }

    public async Task<string> SummarizeSelectedMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSelectionModeEnabled)
        {
            return "Turn on selection mode first, then choose the emails you want to summarize.";
        }

        if (selectedMessages.Count == 0)
        {
            return "Select at least one email to summarize.";
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
            StatusText = selectedMessages.Count == 1
                ? "Generating AI summary for 1 selected email..."
                : $"Generating AI summary for {selectedMessages.Count} selected emails...";

            var summary = await emailSummarizer.SummarizeAsync(selectedMessages.ToArray(), cancellationToken);
            StatusText = selectedMessages.Count == 1
                ? "AI summary ready for 1 selected email."
                : $"AI summary ready for {selectedMessages.Count} selected emails.";
            return summary;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<MailMessage> filtered = allMessages;

        filtered = SelectedAccountFilter switch
        {
            "Personal" => filtered.Where(message => message.AccountType == AccountType.Personal),
            "Shared" => filtered.Where(message => message.AccountType == AccountType.Shared),
            _ => filtered
        };

        if (SelectedCategory != "All categories")
        {
            filtered = filtered.Where(message => message.Categories.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase));
        }

        if (ShowPinnedOnly)
        {
            filtered = filtered.Where(message => message.IsPinned);
        }

        if (ShowFlaggedOnly)
        {
            filtered = filtered.Where(message => message.IsFlagged);
        }

        if (ShowRepliesOnly)
        {
            filtered = filtered.Where(message => message.IsReplyToUserSentMessage);
        }

        if (ShowFromMeOnly)
        {
            filtered = filtered.Where(message => message.IsFromUser);
        }

        if (ShowFromWifeOnly)
        {
            filtered = filtered.Where(message => message.IsFromWife);
        }

        var ordered = SelectedSortOption switch
        {
            SortNewestFirst => filtered
                .OrderByDescending(message => message.ReceivedUtc)
                .ThenByDescending(message => message.Priority.Score)
                .ThenBy(message => message.Subject, StringComparer.CurrentCultureIgnoreCase),
            SortOldestFirst => filtered
                .OrderBy(message => message.ReceivedUtc)
                .ThenByDescending(message => message.Priority.Score)
                .ThenBy(message => message.Subject, StringComparer.CurrentCultureIgnoreCase),
            SortUnreadFirst => filtered
                .OrderBy(message => message.IsRead)
                .ThenByDescending(message => message.Priority.Score)
                .ThenByDescending(message => message.ReceivedUtc)
                .ThenBy(message => message.Subject, StringComparer.CurrentCultureIgnoreCase),
            SortSenderAscending => filtered
                .OrderBy(message => message.FromName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(message => message.FromAddress, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(message => message.Priority.Score)
                .ThenByDescending(message => message.ReceivedUtc),
            SortSubjectAscending => filtered
                .OrderBy(message => message.Subject, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(message => message.Priority.Score)
                .ThenByDescending(message => message.ReceivedUtc),
            _ => filtered
                .OrderByDescending(message => message.Priority.Score)
                .ThenByDescending(message => message.ReceivedUtc)
                .ThenBy(message => message.Subject, StringComparer.CurrentCultureIgnoreCase)
        };

        Messages.Clear();
        foreach (var message in ordered)
        {
            Messages.Add(message);
        }

        TrimSelectionToVisibleMessages();

        if (allMessages.Count == 0)
        {
            StatusText = "No email has been loaded yet.";
            return;
        }

        StatusText = Messages.Count == 0
            ? "No emails match the current filters."
            : HasMoreMessages
                ? $"Showing {Messages.Count} important emails. Scroll to load more."
                : $"Showing {Messages.Count} important emails.";
    }

    private void ClearFilters()
    {
        isResettingFilters = true;

        try
        {
            SetProperty(ref selectedAccountFilter, "All accounts", nameof(SelectedAccountFilter));
            SetProperty(ref selectedCategory, "All categories", nameof(SelectedCategory));
            SetProperty(ref selectedSortOption, SortHighestScore, nameof(SelectedSortOption));
            SetProperty(ref showPinnedOnly, false, nameof(ShowPinnedOnly));
            SetProperty(ref showFlaggedOnly, false, nameof(ShowFlaggedOnly));
            SetProperty(ref showRepliesOnly, false, nameof(ShowRepliesOnly));
            SetProperty(ref showFromMeOnly, false, nameof(ShowFromMeOnly));
            SetProperty(ref showFromWifeOnly, false, nameof(ShowFromWifeOnly));
        }
        finally
        {
            isResettingFilters = false;
        }

        ApplyFilters();
    }

    private void ApplyFiltersIfReady()
    {
        if (!isResettingFilters)
        {
            ApplyFilters();
        }
    }

    private void RebuildCategoryOptions(IReadOnlyList<string>? masterCategories = null)
    {
        var categories = allMessages
            .SelectMany(message => message.Categories)
            .Concat(masterCategories ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        CategoryOptions.Clear();
        CategoryOptions.Add("All categories");
        foreach (var category in categories)
        {
            CategoryOptions.Add(category);
        }

        if (!CategoryOptions.Contains(SelectedCategory))
        {
            SelectedCategory = "All categories";
        }
    }

    private bool CanLoadMoreMessages()
    {
        return !IsBusy && !IsLoadingMore && HasMoreMessages;
    }

    private void ReplaceAllMessages(IEnumerable<MailMessage> messages)
    {
        allMessages.Clear();
        allMessages.AddRange(messages);
        ClearSelection();
    }

    private void TrimSelectionToVisibleMessages()
    {
        if (selectedMessages.Count == 0)
        {
            return;
        }

        var visibleMessageIds = Messages.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
        selectedMessages.RemoveAll(message => !visibleMessageIds.Contains(message.Id));
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectionStatusText = !IsSelectionModeEnabled
            ? "Selection mode is off. Turn it on to pick emails for an AI summary."
            : selectedMessages.Count == 0
                ? "No emails selected yet. Tap emails to add them to the AI summary."
                : selectedMessages.Count == 1
                    ? "1 email selected for the AI summary."
                    : $"{selectedMessages.Count} emails selected for the AI summary.";

        OnPropertyChanged(nameof(SelectedMessageCount));
        OnPropertyChanged(nameof(CanSummarizeSelectedMessages));
    }
}
