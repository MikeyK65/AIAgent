using AIAgent.Models;

namespace AIAgent.Services;

public interface IMessageScorer
{
    PriorityScoreResult Score(MailMessage message);
}
