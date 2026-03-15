using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<SettingsViewModel>();
    }
}
