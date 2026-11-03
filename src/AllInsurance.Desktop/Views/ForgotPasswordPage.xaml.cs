using AllInsurance.Desktop.ViewModels;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class ForgotPasswordPage : Page
{
    public ForgotPasswordViewModel ViewModel { get; }

    public ForgotPasswordPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ForgotPasswordViewModel>();
        DataContext = ViewModel;
        Loaded += (_, _) => ButtonMotion.AttachTree(this);
    }

    private void RecoveryCodeChanged(object sender, RoutedEventArgs e) =>
        ViewModel.RecoveryCode = RecoveryCodeInput.Password;

    private void NewPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.NewPassword = NewPasswordInput.Password;

    private void ConfirmationChanged(object sender, RoutedEventArgs e) =>
        ViewModel.Confirmation = ConfirmationInput.Password;
}
