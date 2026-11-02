using AllInsurance.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class ForcedPasswordChangePage : Page
{
    public ForcedPasswordChangeViewModel ViewModel { get; }

    public ForcedPasswordChangePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ForcedPasswordChangeViewModel>();
        DataContext = ViewModel;
    }

    private void NewPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.NewPassword = NewPasswordInput.Password;

    private void ConfirmationChanged(object sender, RoutedEventArgs e) =>
        ViewModel.Confirmation = ConfirmationInput.Password;
}
