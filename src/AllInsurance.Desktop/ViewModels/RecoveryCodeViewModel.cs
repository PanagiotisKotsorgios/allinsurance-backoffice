using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace AllInsurance.Desktop.ViewModels;

public partial class RecoveryCodeViewModel : ObservableObject
{
    private readonly RecoveryCodeDisplayState _state;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISessionService _session;
    private readonly NavigationService _navigation;
    private readonly ILogger<RecoveryCodeViewModel> _logger;
    private readonly RecoveryCodeDisplay _display;

    [ObservableProperty]
    public partial bool Acknowledged { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string Code => _display.Code;

    public string Heading => _display.Flow switch
    {
        RecoveryCodeFlow.AdministratorSetup => "Ο κωδικός ανάκτησής σας",
        RecoveryCodeFlow.PasswordRecovery => "Ο νέος κωδικός ανάκτησής σας",
        RecoveryCodeFlow.ForcedPasswordChange => "Ο κωδικός ανάκτησής σας",
        RecoveryCodeFlow.OwnCodeRotation => "Ο νέος κωδικός ανάκτησής σας",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string ContinueLabel => _display.Flow == RecoveryCodeFlow.ForcedPasswordChange
        ? "Συνέχεια στην εφαρμογή"
        : "Συνέχεια στη σύνδεση";

    public RecoveryCodeViewModel(
        RecoveryCodeDisplayState state,
        IAuthenticationService authenticationService,
        ISessionService session,
        NavigationService navigation,
        ILogger<RecoveryCodeViewModel> logger)
    {
        _state = state;
        _authenticationService = authenticationService;
        _session = session;
        _navigation = navigation;
        _logger = logger;
        _display = state.Current ?? throw new InvalidOperationException("No one-time recovery code is available.");
    }

    [RelayCommand]
    private void CopyCode()
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(Code);
        Clipboard.SetContent(dataPackage);
        StatusMessage = "Ο κωδικός ανάκτησης αντιγράφηκε.";
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        StatusMessage = string.Empty;
        if (!Acknowledged)
        {
            StatusMessage = "Επιβεβαιώστε ότι αποθηκεύσατε τον κωδικό ανάκτησης πριν συνεχίσετε.";
            return;
        }

        IsBusy = true;
        try
        {
            var flow = _display.Flow;
            if (flow == RecoveryCodeFlow.ForcedPasswordChange)
            {
                await _authenticationService.AcknowledgeRequiredPasswordChangeAsync();
            }

            _state.Clear();
            _navigation.NavigateTo(flow == RecoveryCodeFlow.ForcedPasswordChange
                ? AppRoute.Workspace
                : _session.IsAuthenticated ? AppRoute.Workspace : AppRoute.Login);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Recovery code acknowledgment was rejected.");
            StatusMessage = "Η αλλαγή κωδικού δεν είναι πλέον έγκυρη. Κλείστε την εφαρμογή και συνδεθείτε ξανά.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
