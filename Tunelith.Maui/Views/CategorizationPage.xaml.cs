using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class CategorizationPage : ContentPage
{
	public CategorizationPage(CategorizationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
