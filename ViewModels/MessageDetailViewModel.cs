using AIAgent.Models;
using AIAgent.Services;

namespace AIAgent.ViewModels;

public sealed class MessageDetailViewModel : BaseViewModel
{
  private readonly MessageAggregationService messageAggregationService;
    private readonly IEmailSummarizer emailSummarizer;
    private MailMessage? selectedMessage;
    private string summaryText = "Generate an optional local summary for the selected email.";

 public MessageDetailViewModel(MessageAggregationService messageAggregationService, IEmailSummarizer emailSummarizer)
    {
     this.messageAggregationService = messageAggregationService;
        this.emailSummarizer = emailSummarizer;
        Title = "Email detail";
        SummarizeCommand = new Command(async () => await SummarizeAsync(), () => SelectedMessage is not null && !IsBusy);
    }

    public MailMessage? SelectedMessage
    {
        get => selectedMessage;
        private set
        {
            if (SetProperty(ref selectedMessage, value))
            {
                SummaryText = "Generate an optional local summary for the selected email.";
                SummarizeCommand.ChangeCanExecute();
            }
        }
    }

    public string SummaryText
    {
        get => summaryText;
        set => SetProperty(ref summaryText, value);
    }

    public Command SummarizeCommand { get; }

    public Task LoadAsync(string messageId)
    {
        SelectedMessage = messageAggregationService.GetMessageById(messageId);
        SummaryText = SelectedMessage is null
            ? "The email could not be found in the current cache. Refresh the inbox and try again."
            : "Generate an optional local summary for the selected email.";

        return Task.CompletedTask;
    }

    private async Task SummarizeAsync()
    {
        if (SelectedMessage is null || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SummaryText = "Generating summary...";
            var message = SelectedMessage;
            if (message is null)
            {
                SummaryText = "The selected email is no longer available.";
                return;
            }

            SummaryText = await emailSummarizer.SummarizeAsync(message);
        }
        finally
        {
            IsBusy = false;
            SummarizeCommand.ChangeCanExecute();
        }
    }
}
