using AllInsurance.Application.Authentication;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllInsurance.Desktop.ViewModels;

public partial class ForgotPasswordViewModel(
    IRecoveryService recoveryService,
    RecoveryCodeDisplayState recoveryCodeState,
    NavigationService navigation,
    ILogger<ForgotPasswordViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecoveryCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Confirmation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        ErrorMessage = string.Empty;
        if (!string.Equals(NewPassword, Confirmation, StringComparison.Ordinal))
        {
            ErrorMessage = "Οι κωδικοί πρόσβασης δεν ταιριάζουν.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await recoveryService.ResetPasswordAsync(
                Username,
                RecoveryCode,
                NewPassword,
                Confirmation);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Message;
                return;
            }

            recoveryCodeState.Show(
                result.ReplacementCode ?? throw new InvalidOperationException("Password recovery did not issue a replacement code."),
                RecoveryCodeFlow.PasswordRecovery);
            RecoveryCode = string.Empty;
            NewPassword = string.Empty;
            Confirmation = string.Empty;
            navigation.NavigateTo(AppRoute.RecoveryCode);
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message.Split(Environment.NewLine)[0];
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Password recovery could not be saved.");
            ErrorMessage = "Δεν ήταν δυνατή η επαναφορά κωδικού στη βάση δεδομένων. Δοκιμάστε ξανά.";
        }
        catch (SqliteException exception)
        {
            logger.LogError(exception, "SQLite failed during password recovery.");
            ErrorMessage = "Η επαναφορά κωδικού δεν είναι διαθέσιμη επειδή δεν ήταν δυνατή η πρόσβαση στη βάση δεδομένων.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToSignIn() => navigation.NavigateTo(AppRoute.Login);
}
