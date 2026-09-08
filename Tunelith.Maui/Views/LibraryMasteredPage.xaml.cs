using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class LibraryMasteredPage : ContentPage
{
	private readonly LibraryMasteredViewModel _viewModel;

	public LibraryMasteredPage(LibraryMasteredViewModel viewModel)
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
