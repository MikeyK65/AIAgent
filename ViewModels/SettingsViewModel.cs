using AIAgent.Models;
using AIAgent.Services;

namespace AIAgent.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
 private readonly ISettingsService settingsService;
    private bool useLiveOutlook;
    private string outlookClientId = string.Empty;
    private string personalMailboxAddress = string.Empty;
    private string sharedMailboxAddress = string.Empty;
    private int messageFetchLimit = 25;
    private bool includeInboxMessages = true;
    private bool includeSentMessages = true;
    private bool includeFlaggedMessages = true;
    private string userEmailAddress = string.Empty;
    private string wifeEmailAddress = string.Empty;
    private int pinnedWeight = 100;
    private int flaggedWeight = 80;
    private int replyWeight = 70;
    private int fromUserWeight = 30;
    private int fromWifeWeight = 30;
    private int categoryWeightPerCategory = 10;
    private int recentWithin12HoursWeight = 20;
    private int recentWithin24HoursWeight = 15;
    private int recentWithin72HoursWeight = 10;
    private int recentWithin168HoursWeight = 5;
    private bool enableAiSummaries;
    private string ollamaBaseUrl = string.Empty;
    private string ollamaModel = string.Empty;
    private string statusText = "Configure Outlook Graph access, retrieval folders, scoring weights, sender identities, and optional local AI settings.";

  public SettingsViewModel(ISettingsService settingsService)
    {
     this.settingsService = settingsService;
        Title = "Settings";
        SaveCommand = new Command(SaveSettings);
        LoadSettings();
    }

    public Command SaveCommand { get; }

    public bool UseLiveOutlook
    {
        get => useLiveOutlook;
        set => SetProperty(ref useLiveOutlook, value);
    }

    public string OutlookClientId
    {
        get => outlookClientId;
        set => SetProperty(ref outlookClientId, value);
    }

    public string PersonalMailboxAddress
    {
        get => personalMailboxAddress;
        set => SetProperty(ref personalMailboxAddress, value);
    }

    public string SharedMailboxAddress
    {
        get => sharedMailboxAddress;
        set => SetProperty(ref sharedMailboxAddress, value);
    }

    public int MessageFetchLimit
    {
        get => messageFetchLimit;
        set => SetProperty(ref messageFetchLimit, value);
    }

    public bool IncludeInboxMessages
    {
        get => includeInboxMessages;
        set => SetProperty(ref includeInboxMessages, value);
    }

    public bool IncludeSentMessages
    {
        get => includeSentMessages;
        set => SetProperty(ref includeSentMessages, value);
    }

    public bool IncludeFlaggedMessages
    {
        get => includeFlaggedMessages;
        set => SetProperty(ref includeFlaggedMessages, value);
    }

    public string UserEmailAddress
    {
        get => userEmailAddress;
        set => SetProperty(ref userEmailAddress, value);
    }

    public string WifeEmailAddress
    {
        get => wifeEmailAddress;
        set => SetProperty(ref wifeEmailAddress, value);
    }

    public int PinnedWeight
    {
        get => pinnedWeight;
        set => SetProperty(ref pinnedWeight, value);
    }

    public int FlaggedWeight
    {
        get => flaggedWeight;
        set => SetProperty(ref flaggedWeight, value);
    }

    public int ReplyWeight
    {
        get => replyWeight;
        set => SetProperty(ref replyWeight, value);
    }

    public int FromUserWeight
    {
        get => fromUserWeight;
        set => SetProperty(ref fromUserWeight, value);
    }

    public int FromWifeWeight
    {
        get => fromWifeWeight;
        set => SetProperty(ref fromWifeWeight, value);
    }

    public int CategoryWeightPerCategory
    {
        get => categoryWeightPerCategory;
        set => SetProperty(ref categoryWeightPerCategory, value);
    }

    public int RecentWithin12HoursWeight
    {
        get => recentWithin12HoursWeight;
        set => SetProperty(ref recentWithin12HoursWeight, value);
    }

    public int RecentWithin24HoursWeight
    {
        get => recentWithin24HoursWeight;
        set => SetProperty(ref recentWithin24HoursWeight, value);
    }

    public int RecentWithin72HoursWeight
    {
        get => recentWithin72HoursWeight;
        set => SetProperty(ref recentWithin72HoursWeight, value);
    }

    public int RecentWithin168HoursWeight
    {
        get => recentWithin168HoursWeight;
        set => SetProperty(ref recentWithin168HoursWeight, value);
    }

    public bool EnableAiSummaries
    {
        get => enableAiSummaries;
        set => SetProperty(ref enableAiSummaries, value);
    }

    public string OllamaBaseUrl
    {
        get => ollamaBaseUrl;
        set => SetProperty(ref ollamaBaseUrl, value);
    }

    public string OllamaModel
    {
        get => ollamaModel;
        set => SetProperty(ref ollamaModel, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public void LoadSettings()
    {
        var settings = settingsService.GetSettings();
        UseLiveOutlook = settings.UseLiveOutlook;
        OutlookClientId = settings.OutlookClientId;
        PersonalMailboxAddress = settings.PersonalMailboxAddress;
        SharedMailboxAddress = settings.SharedMailboxAddress;
        MessageFetchLimit = settings.MessageFetchLimit;
        IncludeInboxMessages = settings.IncludeInboxMessages;
        IncludeSentMessages = settings.IncludeSentMessages;
        IncludeFlaggedMessages = settings.IncludeFlaggedMessages;
        UserEmailAddress = settings.UserEmailAddress;
        WifeEmailAddress = settings.WifeEmailAddress;
        PinnedWeight = settings.PinnedWeight;
        FlaggedWeight = settings.FlaggedWeight;
        ReplyWeight = settings.ReplyWeight;
        FromUserWeight = settings.FromUserWeight;
        FromWifeWeight = settings.FromWifeWeight;
        CategoryWeightPerCategory = settings.CategoryWeightPerCategory;
        RecentWithin12HoursWeight = settings.RecentWithin12HoursWeight;
        RecentWithin24HoursWeight = settings.RecentWithin24HoursWeight;
        RecentWithin72HoursWeight = settings.RecentWithin72HoursWeight;
        RecentWithin168HoursWeight = settings.RecentWithin168HoursWeight;
        EnableAiSummaries = settings.EnableAiSummaries;
        OllamaBaseUrl = settings.OllamaBaseUrl;
        OllamaModel = settings.OllamaModel;
    }

    private void SaveSettings()
    {
        if (UseLiveOutlook && (string.IsNullOrWhiteSpace(OutlookClientId)
            || string.IsNullOrWhiteSpace(PersonalMailboxAddress)
            || string.IsNullOrWhiteSpace(SharedMailboxAddress)))
        {
            StatusText = "To enable live Outlook mode, enter the Azure app client ID plus both mailbox addresses.";
            return;
        }

        if (!IncludeInboxMessages && !IncludeSentMessages && !IncludeFlaggedMessages)
        {
            StatusText = "Select at least one folder source to include in retrieval.";
            return;
        }

        settingsService.SaveSettings(new AppSettings
        {
            UseLiveOutlook = UseLiveOutlook,
            OutlookClientId = OutlookClientId,
            PersonalMailboxAddress = PersonalMailboxAddress,
            SharedMailboxAddress = SharedMailboxAddress,
            MessageFetchLimit = Math.Max(5, MessageFetchLimit),
            IncludeInboxMessages = IncludeInboxMessages,
            IncludeSentMessages = IncludeSentMessages,
            IncludeFlaggedMessages = IncludeFlaggedMessages,
            UserEmailAddress = UserEmailAddress,
            WifeEmailAddress = WifeEmailAddress,
            PinnedWeight = PinnedWeight,
            FlaggedWeight = FlaggedWeight,
            ReplyWeight = ReplyWeight,
            FromUserWeight = FromUserWeight,
            FromWifeWeight = FromWifeWeight,
            CategoryWeightPerCategory = CategoryWeightPerCategory,
            RecentWithin12HoursWeight = RecentWithin12HoursWeight,
            RecentWithin24HoursWeight = RecentWithin24HoursWeight,
            RecentWithin72HoursWeight = RecentWithin72HoursWeight,
            RecentWithin168HoursWeight = RecentWithin168HoursWeight,
            EnableAiSummaries = EnableAiSummaries,
            OllamaBaseUrl = OllamaBaseUrl,
            OllamaModel = OllamaModel
        });

        StatusText = UseLiveOutlook
            ? "Settings saved. Refresh the inbox to apply updated retrieval folders and scoring weights. Native Outlook pin state may still be unavailable through Graph."
            : "Settings saved. Refresh the inbox to re-score messages using the updated identities and weights.";
    }
}
