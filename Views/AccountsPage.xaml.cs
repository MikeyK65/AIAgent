using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel viewModel;

    public AccountsPage()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetService<AccountsViewModel>();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }
}
