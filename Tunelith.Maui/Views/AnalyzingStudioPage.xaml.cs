using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class AnalyzingStudioPage : ContentPage
{
    private readonly AnalyzingStudioViewModel _viewModel;

    public AnalyzingStudioPage(AnalyzingStudioViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RunAnalysisAsync();
    }
}
