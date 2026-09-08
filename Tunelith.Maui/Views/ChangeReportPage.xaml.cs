using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class ChangeReportPage : ContentPage
{
	private readonly ChangeReportViewModel _viewModel;

	public ChangeReportPage(ChangeReportViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.InitializeFromSession();
	}
}
