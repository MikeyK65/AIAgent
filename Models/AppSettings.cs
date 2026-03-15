namespace AIAgent.Models;

public sealed class AppSettings
{
    public bool UseLiveOutlook { get; set; }
    public string OutlookClientId { get; set; } = string.Empty;
    public string PersonalMailboxAddress { get; set; } = "mike.personal@hotmail.com";
    public string SharedMailboxAddress { get; set; } = "family.shared@hotmail.com";
    public int MessageFetchLimit { get; set; } = 25;
    public bool IncludeInboxMessages { get; set; } = true;
    public bool IncludeSentMessages { get; set; } = true;
    public bool IncludeFlaggedMessages { get; set; } = true;
    public string UserEmailAddress { get; set; } = "mike.personal@hotmail.com";
    public string WifeEmailAddress { get; set; } = "anna.family@hotmail.com";
    public int PinnedWeight { get; set; } = 100;
    public int FlaggedWeight { get; set; } = 80;
    public int ReplyWeight { get; set; } = 70;
    public int FromUserWeight { get; set; } = 30;
    public int FromWifeWeight { get; set; } = 30;
    public int CategoryWeightPerCategory { get; set; } = 10;
    public int RecentWithin12HoursWeight { get; set; } = 20;
    public int RecentWithin24HoursWeight { get; set; } = 15;
    public int RecentWithin72HoursWeight { get; set; } = 10;
    public int RecentWithin168HoursWeight { get; set; } = 5;
    public bool EnableAiSummaries { get; set; }
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";

    public bool HasLiveOutlookConfiguration =>
        UseLiveOutlook
        && !string.IsNullOrWhiteSpace(OutlookClientId)
        && !string.IsNullOrWhiteSpace(PersonalMailboxAddress)
        && !string.IsNullOrWhiteSpace(SharedMailboxAddress);
}
