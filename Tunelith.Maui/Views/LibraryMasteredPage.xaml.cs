using Tunelith.Maui.ViewModels;

namespace Tunelith.Maui.Views;

public partial class LibraryMasteredPage : ContentPage
{
    public LibraryMasteredPage(LibraryMasteredViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
