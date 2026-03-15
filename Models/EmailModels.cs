namespace AIAgent.Models;

public enum AccountType
{
    Personal,
    Shared
}

public sealed class EmailAccount
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public string ProviderName { get; init; } = "Outlook";
    public bool IsConnected { get; init; }
    public string Status => IsConnected ? "Connected" : "Not connected";
    public string StatusDetail { get; init; } = string.Empty;
}

public sealed class MailMessage
{
    public string Id { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public DateTimeOffset ReceivedUtc { get; init; }
    public bool IsFlagged { get; init; }
    public bool IsPinned { get; init; }
    public bool IsRead { get; init; }
    public bool IsReplyToUserSentMessage { get; set; }
    public string ConversationId { get; init; } = string.Empty;
    public string SourceFolder { get; init; } = "Inbox";
    public string WebLink { get; init; } = string.Empty;
    public List<string> Categories { get; init; } = new();
    public PriorityScoreResult Priority { get; set; } = PriorityScoreResult.Empty;
    public bool IsFromUser { get; set; }
    public bool IsFromWife { get; set; }

    public string AccountLabel => AccountType == AccountType.Shared ? "Shared" : "Personal";
    public string SenderLine => string.IsNullOrWhiteSpace(FromName) ? FromAddress : $"{FromName} <{FromAddress}>";
    public string ReceivedText => ReceivedUtc.ToLocalTime().ToString("ddd d MMM, h:mm tt");
    public string CategoriesText => Categories.Count == 0 ? "No categories" : string.Join(", ", Categories);
    public string ImportanceReasonsText => Priority.Signals.Count == 0 ? "Recent message" : string.Join(" • ", Priority.Signals.Select(signal => signal.Reason));
}

public sealed class MailMessageBatch
{
    public IReadOnlyList<MailMessage> Messages { get; init; } = [];
    public bool MayHaveMore { get; init; }
}

public sealed class MailMessagePage
{
    public IReadOnlyList<MailMessage> Messages { get; init; } = [];
    public bool HasMoreMessages { get; init; }
}
