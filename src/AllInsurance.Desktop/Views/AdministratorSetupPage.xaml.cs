using AllInsurance.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace AllInsurance.Desktop.Views;

public sealed partial class AdministratorSetupPage : Page
{
    public AdministratorSetupViewModel ViewModel { get; }

    public AdministratorSetupPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AdministratorSetupViewModel>();
        DataContext = ViewModel;
    }

    private void PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = PasswordInput.Password;

    private void ConfirmationChanged(object sender, RoutedEventArgs e) =>
        ViewModel.Confirmation = ConfirmationInput.Password;

    private void ShowPasswordsChanged(object sender, RoutedEventArgs e)
    {
        var show = sender is CheckBox { IsChecked: true };
        PasswordInput.PasswordRevealMode = show ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        ConfirmationInput.PasswordRevealMode = show ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
    }
}
