using AllInsurance.Application.Authentication;
using AllInsurance.Desktop.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllInsurance.Desktop.ViewModels;

public partial class LoginViewModel(
    IAuthenticationService authenticationService,
    NavigationService navigation,
    ILogger<LoginViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task SignInAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await authenticationService.SignInAsync(Username, Password);
            Password = string.Empty;
            switch (result.Status)
            {
                case SignInStatus.Authenticated:
                    navigation.NavigateTo(AppRoute.Workspace);
                    break;
                case SignInStatus.PasswordChangeRequired:
                    navigation.NavigateTo(AppRoute.ForcedPasswordChange);
                    break;
                default:
                    ErrorMessage = result.Message;
                    break;
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Sign-in state could not be saved.");
            ErrorMessage = "Η σύνδεση δεν είναι διαθέσιμη επειδή δεν ήταν δυνατή η ενημέρωση της τοπικής βάσης δεδομένων.";
        }
        catch (SqliteException exception)
        {
            logger.LogError(exception, "SQLite failed during sign-in.");
            ErrorMessage = "Η σύνδεση δεν είναι διαθέσιμη επειδή δεν ήταν δυνατή η πρόσβαση στην τοπική βάση δεδομένων.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenForgotPassword() => navigation.NavigateTo(AppRoute.ForgotPassword);
}
