using AllInsurance.Application.Authentication;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllInsurance.Desktop.ViewModels;

public partial class ForcedPasswordChangeViewModel(
    IAuthenticationService authenticationService,
    RecoveryCodeDisplayState recoveryCodeState,
    NavigationService navigation,
    ILogger<ForcedPasswordChangeViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Confirmation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task ChangePasswordAsync()
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
            var code = await authenticationService.CompleteRequiredPasswordChangeAsync(NewPassword, Confirmation);
            if (code is null)
            {
                throw new InvalidOperationException("Password change did not issue a recovery code.");
            }

            recoveryCodeState.Show(code, RecoveryCodeFlow.ForcedPasswordChange);
            NewPassword = string.Empty;
            Confirmation = string.Empty;
            navigation.NavigateTo(AppRoute.RecoveryCode);
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message.Split(Environment.NewLine)[0];
        }
        catch (UnauthorizedAccessException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Required password change could not be saved.");
            ErrorMessage = "Δεν ήταν δυνατή η αποθήκευση του νέου κωδικού. Δοκιμάστε ξανά.";
        }
        catch (SqliteException exception)
        {
            logger.LogError(exception, "SQLite failed during required password change.");
            ErrorMessage = "Η αλλαγή κωδικού δεν είναι διαθέσιμη επειδή δεν ήταν δυνατή η πρόσβαση στη βάση δεδομένων.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
