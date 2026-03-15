using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class MessageDetailPage : ContentPage, IQueryAttributable
{
    private readonly MessageDetailViewModel viewModel;

    public MessageDetailPage()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetService<MessageDetailViewModel>();
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("messageId", out var messageIdValue) && messageIdValue is string messageId)
        {
            await viewModel.LoadAsync(messageId);
        }
    }
}
