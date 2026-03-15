using AIAgent.Models;

namespace AIAgent.Services;

public sealed class MockEmailProvider : IEmailProvider
{
    private static readonly IReadOnlyList<EmailAccount> Accounts =
    [
        new EmailAccount
        {
            Id = "personal",
            DisplayName = "Mike Personal",
            Address = "mike.personal@hotmail.com",
            AccountType = AccountType.Personal,
            ProviderName = "Outlook / Hotmail",
            IsConnected = true
        },
        new EmailAccount
        {
            Id = "shared",
            DisplayName = "Family Shared",
            Address = "family.shared@hotmail.com",
            AccountType = AccountType.Shared,
            ProviderName = "Outlook / Hotmail",
            IsConnected = true
        }
    ];

    private static readonly IReadOnlyList<string> Categories =
    [
        "Finance",
        "Health",
        "Home",
        "Kids",
        "Pets",
        "Sport",
        "Travel",
        "Urgent"
    ];

    public Task<IReadOnlyList<EmailAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Accounts);
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Categories);
    }

    public Task<MailMessageBatch> GetMessageBatchAsync(int fetchLimit, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<MailMessage> messages =
        [
            new MailMessage
            {
                Id = "msg-1",
                AccountId = "shared",
                AccountType = AccountType.Shared,
                Subject = "Vet appointment confirmed for Luna",
                FromName = "City Vet",
                FromAddress = "bookings@cityvet.example",
                Preview = "Your appointment is booked for Thursday at 6:15 PM.",
                BodyText = "Hi Mike, your appointment for Luna is confirmed for Thursday at 6:15 PM. Please bring the insurance card.",
                ReceivedUtc = now.AddHours(-2),
                IsPinned = true,
                IsFlagged = true,
                SourceFolder = "Inbox",
                Categories = ["Pets"]
            },
            new MailMessage
            {
                Id = "msg-2",
                AccountId = "shared",
                AccountType = AccountType.Shared,
                Subject = "Re: Weekend football registration",
                FromName = "Junior League",
                FromAddress = "noreply@juniorleague.example",
                Preview = "Your registration details have been updated.",
                BodyText = "Thanks for your registration. We have updated the weekend football details and attached the revised schedule.",
                ReceivedUtc = now.AddHours(-6),
                IsReplyToUserSentMessage = true,
                SourceFolder = "Inbox",
                Categories = ["Sport"]
            },
            new MailMessage
            {
                Id = "msg-3",
                AccountId = "personal",
                AccountType = AccountType.Personal,
                Subject = "School trip payment reminder",
                FromName = "Riverside School",
                FromAddress = "office@riverside-school.example",
                Preview = "Payment is due this Friday.",
                BodyText = "Just a reminder that the trip payment is due this Friday. Please complete the payment portal before 5 PM.",
                ReceivedUtc = now.AddHours(-20),
                IsFlagged = true,
                SourceFolder = "Inbox",
                Categories = ["Kids", "Finance"]
            },
            new MailMessage
            {
                Id = "msg-4",
                AccountId = "personal",
                AccountType = AccountType.Personal,
                Subject = "Fwd: Dentist forms",
                FromName = "Mike",
                FromAddress = "mike.personal@hotmail.com",
                Preview = "Forwarding the forms to keep handy.",
                BodyText = "Forwarding the forms so they stay near the top of the important view.",
                ReceivedUtc = now.AddDays(-1),
                SourceFolder = "Sent Items",
                Categories = ["Health"]
            },
            new MailMessage
            {
                Id = "msg-5",
                AccountId = "shared",
                AccountType = AccountType.Shared,
                Subject = "Can you approve the pet insurance renewal?",
                FromName = "Anna",
                FromAddress = "anna.family@hotmail.com",
                Preview = "I have flagged the renewal email for you.",
                BodyText = "I flagged the renewal notice because it expires tomorrow. Can you review the quote tonight?",
                ReceivedUtc = now.AddHours(-10),
                IsPinned = true,
                SourceFolder = "Inbox",
                Categories = ["Pets", "Finance"]
            },
            new MailMessage
            {
                Id = "msg-6",
                AccountId = "shared",
                AccountType = AccountType.Shared,
                Subject = "Re: Family holiday booking",
                FromName = "Beach Cottages",
                FromAddress = "reservations@beachcottages.example",
                Preview = "We have answered your question about the booking change.",
                BodyText = "Thanks for your email. We can move the booking to the second week of August with no fee.",
                ReceivedUtc = now.AddHours(-30),
                IsReplyToUserSentMessage = true,
                SourceFolder = "Inbox",
                Categories = ["Travel"]
            },
            new MailMessage
            {
                Id = "msg-7",
                AccountId = "personal",
                AccountType = AccountType.Personal,
                Subject = "Anna shared a meal plan update",
                FromName = "Anna",
                FromAddress = "anna.family@hotmail.com",
                Preview = "I added the new shopping list items.",
                BodyText = "Added the shopping list items for the week and pinned the grocery email in the shared account.",
                ReceivedUtc = now.AddHours(-4),
                SourceFolder = "Inbox",
                Categories = ["Home"]
            },
            new MailMessage
            {
                Id = "msg-8",
                AccountId = "personal",
                AccountType = AccountType.Personal,
                Subject = "Monthly bank statement ready",
                FromName = "Everyday Bank",
                FromAddress = "alerts@everydaybank.example",
                Preview = "Your latest statement is now available.",
                BodyText = "Your monthly bank statement is now available to view online.",
                ReceivedUtc = now.AddDays(-4),
                SourceFolder = "Inbox",
                Categories = ["Finance"]
            }
        ];

        var requestedCount = Math.Max(5, fetchLimit);
        var batch = messages.Take(requestedCount).ToList();

        return Task.FromResult(new MailMessageBatch
        {
            Messages = batch,
            MayHaveMore = messages.Count > batch.Count
        });
    }
}
