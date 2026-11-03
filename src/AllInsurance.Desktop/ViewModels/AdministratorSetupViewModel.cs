using AllInsurance.Application.Authentication;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllInsurance.Desktop.ViewModels;

public partial class AdministratorSetupViewModel(
    IAdministratorSetupService setupService,
    RecoveryCodeDisplayState recoveryCodeState,
    NavigationService navigation,
    ILogger<AdministratorSetupViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Confirmation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task CreateAdministratorAsync()
    {
        ErrorMessage = string.Empty;
        if (!string.Equals(Password, Confirmation, StringComparison.Ordinal))
        {
            ErrorMessage = "Οι κωδικοί πρόσβασης δεν ταιριάζουν.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await setupService.CreateAdministratorAsync(Username, DisplayName, Password, Confirmation);
            recoveryCodeState.Show(result.RecoveryCode, RecoveryCodeFlow.AdministratorSetup);
            Password = string.Empty;
            Confirmation = string.Empty;
            navigation.NavigateTo(AppRoute.RecoveryCode);
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message.Split(Environment.NewLine)[0];
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Initial administrator could not be saved.");
            ErrorMessage = "Δεν ήταν δυνατή η αποθήκευση του διαχειριστή. Ελέγξτε την πρόσβαση στη βάση δεδομένων και δοκιμάστε ξανά.";
        }
        catch (SqliteException exception)
        {
            logger.LogError(exception, "SQLite rejected initial administrator setup.");
            ErrorMessage = "Η τοπική βάση δεδομένων δεν είναι διαθέσιμη. Ελέγξτε την πρόσβαση και δοκιμάστε ξανά.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
