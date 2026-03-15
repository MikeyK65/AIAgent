using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using AIAgent.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace AIAgent.Services;

public sealed class OutlookGraphEmailProvider(ISettingsService settingsService) : IEmailProvider
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string RedirectUri = "http://localhost";
    private const string TokenCacheFileName = "msal-user-token-cache.bin3";
    private static readonly string[] Scopes = ["Mail.Read", "MailboxSettings.Read", "User.Read"];
    private readonly SemaphoreSlim interactiveAuthLock = new(1, 1);
    private readonly SemaphoreSlim applicationInitializationLock = new(1, 1);
    private IPublicClientApplication? application;
    private string configuredClientId = string.Empty;

    public async Task<IReadOnlyList<EmailAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var settings = GetValidatedSettings();
        var accounts = new List<EmailAccount>();

        foreach (var mailbox in GetMailboxSpecs(settings))
        {
            try
            {
                _ = await AcquireTokenAsync(settings.OutlookClientId, mailbox.Address, cancellationToken);
                accounts.Add(new EmailAccount
                {
                    Id = mailbox.Id,
                    DisplayName = mailbox.DisplayName,
                    Address = mailbox.Address,
                    AccountType = mailbox.AccountType,
                    ProviderName = "Outlook / Hotmail (Graph)",
                    IsConnected = true,
                    StatusDetail = "Connected through Microsoft Graph. Native Outlook folder pin state is not available from this provider yet."
                });
            }
            catch (MsalException ex)
            {
                accounts.Add(new EmailAccount
                {
                    Id = mailbox.Id,
                    DisplayName = mailbox.DisplayName,
                    Address = mailbox.Address,
                    AccountType = mailbox.AccountType,
                    ProviderName = "Outlook / Hotmail (Graph)",
                    IsConnected = false,
                    StatusDetail = BuildAuthenticationErrorMessage(ex)
                });
            }
            catch (InvalidOperationException ex)
            {
                accounts.Add(new EmailAccount
                {
                    Id = mailbox.Id,
                    DisplayName = mailbox.DisplayName,
                    Address = mailbox.Address,
                    AccountType = mailbox.AccountType,
                    ProviderName = "Outlook / Hotmail (Graph)",
                    IsConnected = false,
                    StatusDetail = ex.Message
                });
            }
        }

        return accounts;
    }

    public async Task<MailMessageBatch> GetMessageBatchAsync(int fetchLimit, CancellationToken cancellationToken = default)
    {
        var settings = GetValidatedSettings();
        var messages = new List<MailMessage>();
        var mayHaveMore = false;

        foreach (var mailbox in GetMailboxSpecs(settings))
        {
            var authResult = await AcquireTokenAsync(settings.OutlookClientId, mailbox.Address, cancellationToken);
            var requestedLimit = Math.Max(5, fetchLimit);
            var excludedFolderIds = await TryGetJunkFolderIdsAsync(authResult.AccessToken, mailbox, cancellationToken);
            var sentBatch = settings.IncludeSentMessages
                ? await GetFolderMessagesAsync(authResult.AccessToken, mailbox, "sentitems", "Sent Items", requestedLimit, "sentDateTime desc", cancellationToken)
                : new MailMessageBatch();
            var sentMessages = sentBatch.Messages;
            var sentConversationIds = sentMessages
                .Where(message => !string.IsNullOrWhiteSpace(message.ConversationId))
                .Select(message => message.ConversationId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var inboxBatch = settings.IncludeInboxMessages
                ? await GetFolderMessagesAsync(authResult.AccessToken, mailbox, "inbox", "Inbox", requestedLimit, "receivedDateTime desc", cancellationToken)
                : new MailMessageBatch();
            var inboxMessages = inboxBatch.Messages;
            foreach (var inboxMessage in inboxMessages)
            {
                if (!string.IsNullOrWhiteSpace(inboxMessage.ConversationId)
                    && sentConversationIds.Contains(inboxMessage.ConversationId)
                    && !string.Equals(inboxMessage.FromAddress, mailbox.Address, StringComparison.OrdinalIgnoreCase))
                {
                    inboxMessage.IsReplyToUserSentMessage = true;
                }
            }

            var flaggedBatch = settings.IncludeFlaggedMessages
                ? await TryGetFlaggedMessagesAsync(authResult.AccessToken, mailbox, requestedLimit, excludedFolderIds, cancellationToken)
                : new MailMessageBatch();
            var flaggedMessages = flaggedBatch.Messages;
            var mailboxMessages = new Dictionary<string, MailMessage>(StringComparer.OrdinalIgnoreCase);

            MergeMessages(mailboxMessages, sentMessages);
            MergeMessages(mailboxMessages, flaggedMessages);
            MergeMessages(mailboxMessages, inboxMessages);

            mayHaveMore |= sentBatch.MayHaveMore || inboxBatch.MayHaveMore || flaggedBatch.MayHaveMore;
            messages.AddRange(mailboxMessages.Values);
        }

        return new MailMessageBatch
        {
            Messages = messages,
            MayHaveMore = mayHaveMore
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var settings = GetValidatedSettings();
        var categories = new List<string>();
        Exception? lastException = null;

        foreach (var mailbox in GetMailboxSpecs(settings))
        {
            try
            {
                var authResult = await AcquireTokenAsync(settings.OutlookClientId, mailbox.Address, cancellationToken);
                categories.AddRange(await GetMasterCategoriesAsync(authResult.AccessToken, mailbox, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                Debug.WriteLine($"Master category query failed for {mailbox.Address}: {ex.Message}");
            }
        }

        if (categories.Count == 0 && lastException is not null)
        {
            throw new InvalidOperationException("Unable to load Outlook master categories.", lastException);
        }

        return categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();
    }

    private static IReadOnlyList<MailboxSpec> GetMailboxSpecs(AppSettings settings)
    {
        return
        [
            new MailboxSpec("personal", "Personal Outlook", settings.PersonalMailboxAddress, AccountType.Personal),
            new MailboxSpec("shared", "Shared Outlook", settings.SharedMailboxAddress, AccountType.Shared)
        ];
    }

    private AppSettings GetValidatedSettings()
    {
        var settings = settingsService.GetSettings();
        if (!settings.HasLiveOutlookConfiguration)
        {
            throw new InvalidOperationException("Live Outlook mode requires an Azure app client ID plus personal and shared mailbox addresses.");
        }

        return settings;
    }

    private async Task<IPublicClientApplication> GetApplicationAsync(string clientId, CancellationToken cancellationToken)
    {
        if (application is not null && string.Equals(configuredClientId, clientId, StringComparison.Ordinal))
        {
            return application;
        }

        await applicationInitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (application is not null && string.Equals(configuredClientId, clientId, StringComparison.Ordinal))
            {
                return application;
            }

            var createdApplication = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
                .WithRedirectUri(RedirectUri)
                .Build();

            var cacheDirectory = Path.Combine(FileSystem.Current.AppDataDirectory, "msalcache");
            Directory.CreateDirectory(cacheDirectory);

            var storageProperties = new StorageCreationPropertiesBuilder(TokenCacheFileName, cacheDirectory)
                .Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(createdApplication.UserTokenCache);

            application = createdApplication;
            configuredClientId = clientId;
            return application;
        }
        finally
        {
            applicationInitializationLock.Release();
        }
    }

    private async Task<AuthenticationResult> AcquireTokenAsync(string clientId, string loginHint, CancellationToken cancellationToken)
    {
        var application = await GetApplicationAsync(clientId, cancellationToken);
        var existingAccount = await FindAccountAsync(application, loginHint);
        var silentResult = await TryAcquireTokenSilentAsync(application, existingAccount, cancellationToken);
        if (silentResult is not null)
        {
            return silentResult;
        }

        await interactiveAuthLock.WaitAsync(cancellationToken);
        try
        {
            existingAccount = await FindAccountAsync(application, loginHint);
            silentResult = await TryAcquireTokenSilentAsync(application, existingAccount, cancellationToken);
            if (silentResult is not null)
            {
                return silentResult;
            }

            var interactiveRequest = application
            .AcquireTokenInteractive(Scopes)
            .WithLoginHint(loginHint)
            .WithPrompt(Prompt.SelectAccount);

#if WINDOWS
            var parentWindowHandle = GetParentWindowHandle();
            if (parentWindowHandle != IntPtr.Zero)
            {
                interactiveRequest = interactiveRequest.WithParentActivityOrWindow(parentWindowHandle);
            }
#endif

            return await interactiveRequest.ExecuteAsync(cancellationToken);
        }
        finally
        {
            interactiveAuthLock.Release();
        }
    }

    private static async Task<IAccount?> FindAccountAsync(IPublicClientApplication application, string loginHint)
    {
        var accounts = (await application.GetAccountsAsync()).ToList();
        var matchingAccount = accounts.FirstOrDefault(account =>
            string.Equals(account.Username, loginHint, StringComparison.OrdinalIgnoreCase));

        return matchingAccount ?? accounts.FirstOrDefault();
    }

    private static async Task<AuthenticationResult?> TryAcquireTokenSilentAsync(
        IPublicClientApplication application,
        IAccount? existingAccount,
        CancellationToken cancellationToken)
    {
        if (existingAccount is null)
        {
            return null;
        }

        try
        {
            return await application.AcquireTokenSilent(Scopes, existingAccount).ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

#if WINDOWS
    private static IntPtr GetParentWindowHandle()
    {
        var platformWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        return platformWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
    }
#endif

    private static string BuildAuthenticationErrorMessage(MsalException exception)
    {
        if (Contains(exception.Message, "redirect_uri") || string.Equals(exception.ErrorCode, "invalid_request", StringComparison.OrdinalIgnoreCase))
        {
            return "The Azure app registration is missing the `http://localhost` redirect URI under Mobile and desktop applications.";
        }

        if (string.Equals(exception.ErrorCode, "unauthorized_client", StringComparison.OrdinalIgnoreCase)
            || Contains(exception.Message, "not enabled for consumers")
            || Contains(exception.Message, "personal Microsoft accounts"))
        {
            return "The Azure app registration must allow personal Microsoft accounts for this `consumers` authority.";
        }

        if (string.Equals(exception.ErrorCode, "access_denied", StringComparison.OrdinalIgnoreCase)
            || Contains(exception.Message, "consent"))
        {
            return "The Azure app registration is missing consent for Microsoft Graph delegated permissions such as `Mail.Read`, `MailboxSettings.Read`, and `User.Read`.";
        }

        if (string.Equals(exception.ErrorCode, "invalid_client", StringComparison.OrdinalIgnoreCase))
        {
            return "The saved Outlook client ID does not match a valid public client app registration.";
        }

        return $"Authentication failed ({exception.ErrorCode}): {exception.Message}";
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<MailMessageBatch> GetFolderMessagesAsync(
        string accessToken,
        MailboxSpec mailbox,
        string folderId,
        string sourceFolder,
        int limit,
        string orderBy,
        CancellationToken cancellationToken)
    {
        var requestedLimit = Math.Max(5, limit);
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{GraphBaseUrl}/me/mailFolders/{folderId}/messages?$top={requestedLimit}&$orderby={Uri.EscapeDataString(orderBy)}&$select=id,conversationId,subject,from,receivedDateTime,sentDateTime,bodyPreview,body,flag,isRead,categories,webLink");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Outlook Graph request failed for {mailbox.Address}: {(int)response.StatusCode} {response.ReasonPhrase}. {content}".Trim());
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphMessageListResponse>(cancellationToken: cancellationToken)
            ?? new GraphMessageListResponse();

        return new MailMessageBatch
        {
            Messages = payload.Value.Select(message => ToMailMessage(message, mailbox, sourceFolder)).ToList(),
            MayHaveMore = payload.Value.Count >= requestedLimit
        };
    }

    private static async Task<IReadOnlyList<string>> GetMasterCategoriesAsync(
        string accessToken,
        MailboxSpec mailbox,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBaseUrl}/me/outlook/masterCategories");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Outlook master category request failed for {mailbox.Address}: {(int)response.StatusCode} {response.ReasonPhrase}. {content}".Trim());
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCategoryListResponse>(cancellationToken: cancellationToken)
            ?? new GraphCategoryListResponse();

        return payload.Value
            .Select(category => category.DisplayName)
            .Where(displayName => !string.IsNullOrWhiteSpace(displayName))
            .Select(displayName => displayName!)
            .ToList();
    }

    private static async Task<MailMessageBatch> TryGetFlaggedMessagesAsync(
        string accessToken,
        MailboxSpec mailbox,
        int limit,
        IReadOnlySet<string> excludedFolderIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestedLimit = Math.Max(5, limit);
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{GraphBaseUrl}/me/messages?$top={requestedLimit}&$filter={Uri.EscapeDataString("flag/flagStatus eq 'flagged'")}&$select=id,parentFolderId,conversationId,subject,from,receivedDateTime,sentDateTime,bodyPreview,body,flag,isRead,categories,webLink");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"Flagged message query failed for {mailbox.Address}: {(int)response.StatusCode} {response.ReasonPhrase}. {content}".Trim());
                return new MailMessageBatch();
            }

            var payload = await response.Content.ReadFromJsonAsync<GraphMessageListResponse>(cancellationToken: cancellationToken)
                ?? new GraphMessageListResponse();
            var filteredMessages = payload.Value
                .Where(message => string.IsNullOrWhiteSpace(message.ParentFolderId) || !excludedFolderIds.Contains(message.ParentFolderId))
                .Select(message => ToMailMessage(message, mailbox, "Flagged"))
                .ToList();

            return new MailMessageBatch
            {
                Messages = filteredMessages,
                MayHaveMore = payload.Value.Count >= requestedLimit
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Flagged message query failed for {mailbox.Address}: {ex.Message}");
            return new MailMessageBatch();
        }
    }

    private static async Task<HashSet<string>> TryGetJunkFolderIdsAsync(
        string accessToken,
        MailboxSpec mailbox,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetFolderTreeIdsAsync(accessToken, mailbox, "junkemail", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Junk folder query failed for {mailbox.Address}: {ex.Message}");
            return [];
        }
    }

    private static async Task<HashSet<string>> GetFolderTreeIdsAsync(
        string accessToken,
        MailboxSpec mailbox,
        string folderId,
        CancellationToken cancellationToken)
    {
        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingFolderIds = new Queue<string>();
        pendingFolderIds.Enqueue(folderId);

        while (pendingFolderIds.Count > 0)
        {
            var currentFolderId = pendingFolderIds.Dequeue();
            var folder = await GetMailFolderAsync(accessToken, mailbox, currentFolderId, cancellationToken);
            if (folder is null || string.IsNullOrWhiteSpace(folder.Id) || !folderIds.Add(folder.Id))
            {
                continue;
            }

            foreach (var childFolderId in await GetChildFolderIdsAsync(accessToken, mailbox, currentFolderId, cancellationToken))
            {
                if (!folderIds.Contains(childFolderId))
                {
                    pendingFolderIds.Enqueue(childFolderId);
                }
            }
        }

        return folderIds;
    }

    private static async Task<GraphMailFolder?> GetMailFolderAsync(
        string accessToken,
        MailboxSpec mailbox,
        string folderId,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBaseUrl}/me/mailFolders/{folderId}?$select=id");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Outlook folder request failed for {mailbox.Address}: {(int)response.StatusCode} {response.ReasonPhrase}. {content}".Trim());
        }

        return await response.Content.ReadFromJsonAsync<GraphMailFolder>(cancellationToken: cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> GetChildFolderIdsAsync(
        string accessToken,
        MailboxSpec mailbox,
        string folderId,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBaseUrl}/me/mailFolders/{folderId}/childFolders?$top=100&$select=id");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Outlook child folder request failed for {mailbox.Address}: {(int)response.StatusCode} {response.ReasonPhrase}. {content}".Trim());
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphMailFolderListResponse>(cancellationToken: cancellationToken)
            ?? new GraphMailFolderListResponse();

        return payload.Value
            .Select(folder => folder.Id)
            .Where(folderId => !string.IsNullOrWhiteSpace(folderId))
            .Select(folderId => folderId!)
            .ToList();
    }

    private static void MergeMessages(IDictionary<string, MailMessage> destination, IEnumerable<MailMessage> source)
    {
        foreach (var message in source)
        {
            destination[message.Id] = message;
        }
    }

    private static MailMessage ToMailMessage(GraphMessage message, MailboxSpec mailbox, string sourceFolder)
    {
        var sender = message.From?.EmailAddress;
        var bodyText = string.IsNullOrWhiteSpace(message.Body?.Content)
            ? message.BodyPreview ?? string.Empty
            : WebUtility.HtmlDecode(message.Body.Content);
        var receivedUtc = message.ReceivedDateTime ?? message.SentDateTime ?? DateTimeOffset.UtcNow;

        return new MailMessage
        {
            Id = message.Id ?? Guid.NewGuid().ToString("N"),
            AccountId = mailbox.Id,
            AccountType = mailbox.AccountType,
            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject,
            FromName = sender?.Name ?? mailbox.DisplayName,
            FromAddress = sender?.Address ?? mailbox.Address,
            Preview = message.BodyPreview ?? string.Empty,
            BodyText = bodyText,
            ReceivedUtc = receivedUtc,
            IsFlagged = string.Equals(message.Flag?.FlagStatus, "flagged", StringComparison.OrdinalIgnoreCase),
            IsPinned = false,
            IsRead = message.IsRead ?? false,
            ConversationId = message.ConversationId ?? string.Empty,
            SourceFolder = sourceFolder,
            WebLink = message.WebLink ?? string.Empty,
            Categories = message.Categories ?? []
        };
    }

    private sealed record MailboxSpec(string Id, string DisplayName, string Address, AccountType AccountType);

    private sealed class GraphMessageListResponse
    {
        [JsonPropertyName("value")]
        public List<GraphMessage> Value { get; init; } = [];
    }

    private sealed class GraphCategoryListResponse
    {
        [JsonPropertyName("value")]
        public List<GraphCategory> Value { get; init; } = [];
    }

    private sealed class GraphMailFolderListResponse
    {
        [JsonPropertyName("value")]
        public List<GraphMailFolder> Value { get; init; } = [];
    }

    private sealed class GraphCategory
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }
    }

    private sealed class GraphMailFolder
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class GraphMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("parentFolderId")]
        public string? ParentFolderId { get; init; }

        [JsonPropertyName("conversationId")]
        public string? ConversationId { get; init; }

        [JsonPropertyName("subject")]
        public string? Subject { get; init; }

        [JsonPropertyName("from")]
        public GraphRecipient? From { get; init; }

        [JsonPropertyName("receivedDateTime")]
        public DateTimeOffset? ReceivedDateTime { get; init; }

        [JsonPropertyName("sentDateTime")]
        public DateTimeOffset? SentDateTime { get; init; }

        [JsonPropertyName("bodyPreview")]
        public string? BodyPreview { get; init; }

        [JsonPropertyName("body")]
        public GraphBody? Body { get; init; }

        [JsonPropertyName("flag")]
        public GraphFlag? Flag { get; init; }

        [JsonPropertyName("isRead")]
        public bool? IsRead { get; init; }

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; init; }

        [JsonPropertyName("webLink")]
        public string? WebLink { get; init; }
    }

    private sealed class GraphRecipient
    {
        [JsonPropertyName("emailAddress")]
        public GraphEmailAddress? EmailAddress { get; init; }
    }

    private sealed class GraphEmailAddress
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("address")]
        public string? Address { get; init; }
    }

    private sealed class GraphBody
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class GraphFlag
    {
        [JsonPropertyName("flagStatus")]
        public string? FlagStatus { get; init; }
    }
}
