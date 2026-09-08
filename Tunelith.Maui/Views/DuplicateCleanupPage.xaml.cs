using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class DuplicateCleanupPage : ContentPage
{
	private readonly DuplicateCleanupViewModel _viewModel;

	public DuplicateCleanupPage(DuplicateCleanupViewModel viewModel)
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
