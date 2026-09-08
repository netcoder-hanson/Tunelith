using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class ScanHistoryPage : ContentPage
{
    private readonly ScanHistoryViewModel _viewModel;

    public ScanHistoryPage(ScanHistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadHistoryAsync();
    }
}
