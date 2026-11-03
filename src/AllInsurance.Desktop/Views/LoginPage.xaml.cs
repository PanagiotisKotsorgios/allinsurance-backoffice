using AllInsurance.Desktop.ViewModels;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace AllInsurance.Desktop.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        DataContext = ViewModel;
        Loaded += (_, _) => ButtonMotion.AttachTree(this);
    }

    private void PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = PasswordInput.Password;

    private void ShowPasswordChanged(object sender, RoutedEventArgs e)
    {
        var show = sender is ToggleButton { IsChecked: true };
        PasswordInput.PasswordRevealMode = show ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ShowPasswordButton,
            show ? "Απόκρυψη κωδικού" : "Εμφάνιση κωδικού");
    }

    private void PasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.SignInCommand.Execute(null);
            e.Handled = true;
        }
    }
}
