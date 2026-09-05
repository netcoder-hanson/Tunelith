using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
