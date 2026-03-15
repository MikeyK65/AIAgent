using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class AzureDevOpsActivityPage : ContentPage
{
    private readonly AzureDevOpsActivityViewModel viewModel;

    public AzureDevOpsActivityPage()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetService<AzureDevOpsActivityViewModel>();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }
}
