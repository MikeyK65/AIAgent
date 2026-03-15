using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class GitHubActivityPage : ContentPage
{
    private readonly GitHubActivityViewModel viewModel;

    public GitHubActivityPage()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetService<GitHubActivityViewModel>();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }
}
