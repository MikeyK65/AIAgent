using System.Collections.ObjectModel;
using AIAgent.Models;
using AIAgent.Services;

namespace AIAgent.ViewModels;

public sealed class AccountsViewModel : BaseViewModel
{
 private readonly MessageAggregationService messageAggregationService;
    private string statusText = "Refresh to load the current Outlook account state.";
    private bool hasLoaded;

  public AccountsViewModel(MessageAggregationService messageAggregationService)
    {
     this.messageAggregationService = messageAggregationService;
        Title = "Accounts";
        RefreshCommand = new Command(async () => await LoadAsync());
    }

    public ObservableCollection<EmailAccount> Accounts { get; } = new();
    public Command RefreshCommand { get; }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public async Task InitializeAsync()
    {
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Refreshing account state...";

            var accounts = await messageAggregationService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            StatusText = $"{Accounts.Count} Outlook accounts available.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
