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
        RecoveryCodeFlow.AdministratorSetup => "Your recovery code",
        RecoveryCodeFlow.PasswordRecovery => "Your new recovery code",
        RecoveryCodeFlow.ForcedPasswordChange => "Your recovery code",
        RecoveryCodeFlow.OwnCodeRotation => "Your new recovery code",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string ContinueLabel => _display.Flow == RecoveryCodeFlow.ForcedPasswordChange
        ? "Continue to All Insurance"
        : "Continue to sign in";

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
        StatusMessage = "Recovery code copied.";
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        StatusMessage = string.Empty;
        if (!Acknowledged)
        {
            StatusMessage = "Confirm that you have stored this recovery code before continuing.";
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
                ? AppRoute.EmptyWorkspace
                : _session.IsAuthenticated ? AppRoute.EmptyWorkspace : AppRoute.Login);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Recovery code acknowledgment was rejected.");
            StatusMessage = "This password-change session is no longer valid. Close the app and sign in again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
