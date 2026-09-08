using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class DuplicateCleanupPage : ContentPage
{
    public DuplicateCleanupPage(DuplicateCleanupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
