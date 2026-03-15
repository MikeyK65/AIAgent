using AIAgent.Models;

namespace AIAgent.Services;

public interface IEmailSummarizer
{
    Task<string> SummarizeAsync(MailMessage message, CancellationToken cancellationToken = default);
    Task<string> SummarizeAsync(IReadOnlyList<MailMessage> messages, CancellationToken cancellationToken = default);
}
