using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class LibraryPage : ContentPage
{
	private readonly LibraryViewModel _viewModel;

	public LibraryPage(LibraryViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.InitializeAsync();
	}
}
