using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Security;
using AllInsurance.Desktop.Views;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Navigation;

public sealed class NavigationService(
    ISessionService session,
    RecoveryCodeDisplayState recoveryCodeState)
{
    private Frame? _frame;

    public void Configure(Frame frame) => _frame = frame;

    public void NavigateTo(AppRoute route)
    {
        var frame = _frame ?? throw new InvalidOperationException("The application window is not ready.");
        if (route == AppRoute.Workspace && !session.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("The workspace is only available after authentication.");
        }

        if (route == AppRoute.ForcedPasswordChange && !session.RequiresPasswordChange)
        {
            throw new UnauthorizedAccessException("A verified temporary-password sign-in is required.");
        }

        if (route == AppRoute.RecoveryCode && recoveryCodeState.Current is null)
        {
            throw new InvalidOperationException("There is no one-time recovery code to display.");
        }

        var pageType = route switch
        {
            AppRoute.AdministratorSetup => typeof(AdministratorSetupPage),
            AppRoute.RecoveryCode => typeof(RecoveryCodePage),
            AppRoute.Login => typeof(LoginPage),
            AppRoute.ForgotPassword => typeof(ForgotPasswordPage),
            AppRoute.ForcedPasswordChange => typeof(ForcedPasswordChangePage),
            AppRoute.Workspace => typeof(WorkspacePage),
            AppRoute.RepairRequired => typeof(RepairRequiredPage),
            AppRoute.StartupFailure => typeof(StartupFailurePage),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };

        if (route == AppRoute.Workspace) App.MaximizeWorkspace();
        frame.Navigate(pageType);
        frame.BackStack.Clear();
    }
}
