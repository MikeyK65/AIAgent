using AIAgent.Models;
using Microsoft.Maui.Storage;

namespace AIAgent.Services;

public sealed class LocalSettingsService : ISettingsService
{
    private const string UseLiveOutlookKey = "use-live-outlook";
    private const string OutlookClientIdKey = "outlook-client-id";
    private const string PersonalMailboxAddressKey = "personal-mailbox-address";
    private const string SharedMailboxAddressKey = "shared-mailbox-address";
    private const string MessageFetchLimitKey = "message-fetch-limit";
    private const string IncludeInboxMessagesKey = "include-inbox-messages";
    private const string IncludeSentMessagesKey = "include-sent-messages";
    private const string IncludeFlaggedMessagesKey = "include-flagged-messages";
    private const string UserEmailKey = "user-email";
    private const string WifeEmailKey = "wife-email";
    private const string PinnedWeightKey = "pinned-weight";
    private const string FlaggedWeightKey = "flagged-weight";
    private const string ReplyWeightKey = "reply-weight";
    private const string FromUserWeightKey = "from-user-weight";
    private const string FromWifeWeightKey = "from-wife-weight";
    private const string CategoryWeightPerCategoryKey = "category-weight-per-category";
    private const string RecentWithin12HoursWeightKey = "recent-within-12-hours-weight";
    private const string RecentWithin24HoursWeightKey = "recent-within-24-hours-weight";
    private const string RecentWithin72HoursWeightKey = "recent-within-72-hours-weight";
    private const string RecentWithin168HoursWeightKey = "recent-within-168-hours-weight";
    private const string AiEnabledKey = "ai-enabled";
    private const string OllamaBaseUrlKey = "ollama-base-url";
    private const string OllamaModelKey = "ollama-model";

    public AppSettings GetSettings()
    {
        return new AppSettings
        {
            UseLiveOutlook = Preferences.Default.Get(UseLiveOutlookKey, false),
            OutlookClientId = Preferences.Default.Get(OutlookClientIdKey, string.Empty),
            PersonalMailboxAddress = Preferences.Default.Get(PersonalMailboxAddressKey, "mike.personal@hotmail.com"),
            SharedMailboxAddress = Preferences.Default.Get(SharedMailboxAddressKey, "family.shared@hotmail.com"),
            MessageFetchLimit = Preferences.Default.Get(MessageFetchLimitKey, 25),
            IncludeInboxMessages = Preferences.Default.Get(IncludeInboxMessagesKey, true),
            IncludeSentMessages = Preferences.Default.Get(IncludeSentMessagesKey, true),
            IncludeFlaggedMessages = Preferences.Default.Get(IncludeFlaggedMessagesKey, true),
            UserEmailAddress = Preferences.Default.Get(UserEmailKey, "mike.personal@hotmail.com"),
            WifeEmailAddress = Preferences.Default.Get(WifeEmailKey, "anna.family@hotmail.com"),
            PinnedWeight = Preferences.Default.Get(PinnedWeightKey, 100),
            FlaggedWeight = Preferences.Default.Get(FlaggedWeightKey, 80),
            ReplyWeight = Preferences.Default.Get(ReplyWeightKey, 70),
            FromUserWeight = Preferences.Default.Get(FromUserWeightKey, 30),
            FromWifeWeight = Preferences.Default.Get(FromWifeWeightKey, 30),
            CategoryWeightPerCategory = Preferences.Default.Get(CategoryWeightPerCategoryKey, 10),
            RecentWithin12HoursWeight = Preferences.Default.Get(RecentWithin12HoursWeightKey, 20),
            RecentWithin24HoursWeight = Preferences.Default.Get(RecentWithin24HoursWeightKey, 15),
            RecentWithin72HoursWeight = Preferences.Default.Get(RecentWithin72HoursWeightKey, 10),
            RecentWithin168HoursWeight = Preferences.Default.Get(RecentWithin168HoursWeightKey, 5),
            EnableAiSummaries = Preferences.Default.Get(AiEnabledKey, false),
            OllamaBaseUrl = Preferences.Default.Get(OllamaBaseUrlKey, "http://localhost:11434"),
            OllamaModel = Preferences.Default.Get(OllamaModelKey, "llama3.2")
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        Preferences.Default.Set(UseLiveOutlookKey, settings.UseLiveOutlook);
        Preferences.Default.Set(OutlookClientIdKey, settings.OutlookClientId ?? string.Empty);
        Preferences.Default.Set(PersonalMailboxAddressKey, settings.PersonalMailboxAddress ?? string.Empty);
        Preferences.Default.Set(SharedMailboxAddressKey, settings.SharedMailboxAddress ?? string.Empty);
        Preferences.Default.Set(MessageFetchLimitKey, Math.Max(5, settings.MessageFetchLimit));
        Preferences.Default.Set(IncludeInboxMessagesKey, settings.IncludeInboxMessages);
        Preferences.Default.Set(IncludeSentMessagesKey, settings.IncludeSentMessages);
        Preferences.Default.Set(IncludeFlaggedMessagesKey, settings.IncludeFlaggedMessages);
        Preferences.Default.Set(UserEmailKey, settings.UserEmailAddress ?? string.Empty);
        Preferences.Default.Set(WifeEmailKey, settings.WifeEmailAddress ?? string.Empty);
        Preferences.Default.Set(PinnedWeightKey, settings.PinnedWeight);
        Preferences.Default.Set(FlaggedWeightKey, settings.FlaggedWeight);
        Preferences.Default.Set(ReplyWeightKey, settings.ReplyWeight);
        Preferences.Default.Set(FromUserWeightKey, settings.FromUserWeight);
        Preferences.Default.Set(FromWifeWeightKey, settings.FromWifeWeight);
        Preferences.Default.Set(CategoryWeightPerCategoryKey, settings.CategoryWeightPerCategory);
        Preferences.Default.Set(RecentWithin12HoursWeightKey, settings.RecentWithin12HoursWeight);
        Preferences.Default.Set(RecentWithin24HoursWeightKey, settings.RecentWithin24HoursWeight);
        Preferences.Default.Set(RecentWithin72HoursWeightKey, settings.RecentWithin72HoursWeight);
        Preferences.Default.Set(RecentWithin168HoursWeightKey, settings.RecentWithin168HoursWeight);
        Preferences.Default.Set(AiEnabledKey, settings.EnableAiSummaries);
        Preferences.Default.Set(OllamaBaseUrlKey, settings.OllamaBaseUrl ?? string.Empty);
        Preferences.Default.Set(OllamaModelKey, settings.OllamaModel ?? string.Empty);
    }
}
