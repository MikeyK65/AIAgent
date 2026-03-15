using AIAgent.Models;
using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent.Views;

public partial class InboxPage : ContentPage
{
    private readonly CombinedInboxViewModel viewModel;
    private readonly CollectionView? messagesCollectionView;

    public InboxPage()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetService<CombinedInboxViewModel>();
        BindingContext = viewModel;
        messagesCollectionView = this.FindByName<CollectionView>("MessagesCollectionView");

        var clearFiltersButton = this.FindByName<Button>("ClearFiltersButton");
        if (clearFiltersButton is not null)
        {
            clearFiltersButton.Clicked += OnClearFiltersClicked;
        }

        var selectionModeCheckBox = this.FindByName<CheckBox>("SelectionModeCheckBox");
        if (selectionModeCheckBox is not null)
        {
            selectionModeCheckBox.CheckedChanged += OnSelectionModeCheckedChanged;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (viewModel.IsSelectionModeEnabled)
        {
            viewModel.UpdateSelection(e.CurrentSelection);
            return;
        }

        viewModel.ClearSelection();

        if (e.CurrentSelection.FirstOrDefault() is not MailMessage message)
        {
            return;
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }

        await viewModel.OpenMessageAsync(message);
    }

    private async void OnClearFiltersClicked(object? sender, EventArgs e)
    {
        var shouldClear = await DisplayAlert("Clear filters", "Clear all selected filters?", "Yes", "No");
        if (shouldClear)
        {
            viewModel.ClearFiltersCommand.Execute(null);
        }
    }

    private void OnSelectionModeCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            return;
        }

        messagesCollectionView?.SelectedItems.Clear();
        if (messagesCollectionView is not null)
        {
            messagesCollectionView.SelectedItem = null;
        }

        viewModel.ClearSelection();
    }

    private async void OnSummarizeSelectedClicked(object? sender, EventArgs e)
    {
        var summary = await viewModel.SummarizeSelectedMessagesAsync();
        await DisplayAlert("AI summary", summary, "Close");
    }
}
