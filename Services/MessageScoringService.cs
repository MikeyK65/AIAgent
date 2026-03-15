using AIAgent.Models;

namespace AIAgent.Services;

public sealed class MessageScoringService(ISettingsService settingsService) : IMessageScorer
{
    public PriorityScoreResult Score(MailMessage message)
    {
        var settings = settingsService.GetSettings();
        var signals = new List<PrioritySignal>();
        var score = 0;
        message.IsFromUser = false;
        message.IsFromWife = false;

        var normalizedSender = message.FromAddress.Trim().ToLowerInvariant();
        var normalizedUser = settings.UserEmailAddress.Trim().ToLowerInvariant();
        var normalizedWife = settings.WifeEmailAddress.Trim().ToLowerInvariant();

        if (message.IsPinned)
        {
            signals.Add(new PrioritySignal { Type = "Pinned", Weight = settings.PinnedWeight, Reason = "Pinned in source folder" });
            score += settings.PinnedWeight;
        }

        if (message.IsFlagged)
        {
            signals.Add(new PrioritySignal { Type = "Flagged", Weight = settings.FlaggedWeight, Reason = "Flagged for follow-up" });
            score += settings.FlaggedWeight;
        }

        if (message.IsReplyToUserSentMessage)
        {
            signals.Add(new PrioritySignal { Type = "Reply", Weight = settings.ReplyWeight, Reason = "Reply to a message you sent" });
            score += settings.ReplyWeight;
        }

        if (!string.IsNullOrWhiteSpace(normalizedUser) && normalizedSender == normalizedUser)
        {
            message.IsFromUser = true;
            signals.Add(new PrioritySignal { Type = "FromUser", Weight = settings.FromUserWeight, Reason = "Sent by you" });
            score += settings.FromUserWeight;
        }

        if (!string.IsNullOrWhiteSpace(normalizedWife) && normalizedSender == normalizedWife)
        {
            message.IsFromWife = true;
            signals.Add(new PrioritySignal { Type = "FromWife", Weight = settings.FromWifeWeight, Reason = "Sent by your wife" });
            score += settings.FromWifeWeight;
        }

        if (message.Categories.Count > 0)
        {
            var categoryWeight = message.Categories.Count * settings.CategoryWeightPerCategory;
            signals.Add(new PrioritySignal
            {
                Type = "Category",
                Weight = categoryWeight,
                Reason = $"Categories: {string.Join(", ", message.Categories)}"
            });
            score += categoryWeight;
        }

        var age = DateTimeOffset.UtcNow - message.ReceivedUtc;
        var recencyWeight = age.TotalHours switch
        {
            <= 12 => settings.RecentWithin12HoursWeight,
            <= 24 => settings.RecentWithin24HoursWeight,
            <= 72 => settings.RecentWithin72HoursWeight,
            <= 168 => settings.RecentWithin168HoursWeight,
            _ => 0
        };

        if (recencyWeight > 0)
        {
            signals.Add(new PrioritySignal { Type = "Recent", Weight = recencyWeight, Reason = "Recent activity" });
            score += recencyWeight;
        }

        return new PriorityScoreResult
        {
            Score = score,
            Signals = signals
        };
    }
}
