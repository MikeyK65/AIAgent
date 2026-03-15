using AIAgent.Models;

namespace AIAgent.Services;

public sealed class MessageAggregationService(IEmailProvider emailProvider, IMessageScorer scorer, ISettingsService settingsService)
{
    private List<MailMessage> cachedMessages = new();
    private List<string> cachedCategories = new();
    private int visibleMessageCount;

    public Task<IReadOnlyList<EmailAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return emailProvider.GetAccountsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        cachedCategories = (await emailProvider.GetCategoriesAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        return cachedCategories;
    }

    public Task<MailMessagePage> RefreshMessagesAsync(CancellationToken cancellationToken = default)
    {
        visibleMessageCount = GetPageSize();
        return GetVisibleMessagesAsync(cancellationToken);
    }

    public Task<MailMessagePage> LoadMoreMessagesAsync(CancellationToken cancellationToken = default)
    {
        visibleMessageCount += GetPageSize();
        return GetVisibleMessagesAsync(cancellationToken);
    }

    private async Task<MailMessagePage> GetVisibleMessagesAsync(CancellationToken cancellationToken)
    {
        visibleMessageCount = Math.Max(GetPageSize(), visibleMessageCount);
        var batch = await emailProvider.GetMessageBatchAsync(visibleMessageCount, cancellationToken);
        var messages = batch.Messages.ToList();

        foreach (var message in messages)
        {
            message.Priority = scorer.Score(message);
        }

        cachedMessages = messages
            .OrderByDescending(message => message.Priority.Score)
            .ThenByDescending(message => message.ReceivedUtc)
            .ToList();

        var visibleMessages = cachedMessages.Take(visibleMessageCount).ToList();

        return new MailMessagePage
        {
            Messages = visibleMessages,
            HasMoreMessages = cachedMessages.Count > visibleMessages.Count || batch.MayHaveMore
        };
    }

    public MailMessage? GetMessageById(string messageId)
    {
        return cachedMessages.FirstOrDefault(message => message.Id == messageId);
    }

    private int GetPageSize()
    {
        return Math.Max(5, settingsService.GetSettings().MessageFetchLimit);
    }
}
