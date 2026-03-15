using AIAgent.Models;

namespace AIAgent.Services;

public interface IEmailProvider
{
    Task<IReadOnlyList<EmailAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<MailMessageBatch> GetMessageBatchAsync(int fetchLimit, CancellationToken cancellationToken = default);
}
