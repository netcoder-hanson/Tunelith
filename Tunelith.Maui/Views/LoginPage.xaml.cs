using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class LoginPage : ContentPage
{
	private readonly LoginViewModel _viewModel;

	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.CheckTermsAcceptedAsync();
	}
}
