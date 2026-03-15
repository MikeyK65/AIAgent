using AIAgent.Models;

namespace AIAgent.Services;

public sealed class ConfiguredEmailProvider(
    ISettingsService settingsService,
    MockEmailProvider mockEmailProvider,
    OutlookGraphEmailProvider outlookGraphEmailProvider) : IEmailProvider
{
    public Task<IReadOnlyList<EmailAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        return UseLiveOutlook(settings)
            ? outlookGraphEmailProvider.GetAccountsAsync(cancellationToken)
            : mockEmailProvider.GetAccountsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        return UseLiveOutlook(settings)
            ? outlookGraphEmailProvider.GetCategoriesAsync(cancellationToken)
            : mockEmailProvider.GetCategoriesAsync(cancellationToken);
    }

    public Task<MailMessageBatch> GetMessageBatchAsync(int fetchLimit, CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        return UseLiveOutlook(settings)
            ? outlookGraphEmailProvider.GetMessageBatchAsync(fetchLimit, cancellationToken)
            : mockEmailProvider.GetMessageBatchAsync(fetchLimit, cancellationToken);
    }

    private static bool UseLiveOutlook(AppSettings settings)
    {
        if (!settings.UseLiveOutlook)
        {
            return false;
        }

        if (!settings.HasLiveOutlookConfiguration)
        {
            throw new InvalidOperationException("Complete the Outlook settings with an Azure app client ID plus personal and shared mailbox addresses before enabling live mode.");
        }

        return true;
    }
}
