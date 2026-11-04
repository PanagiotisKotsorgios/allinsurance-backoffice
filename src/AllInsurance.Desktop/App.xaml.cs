using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Users;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Settings;
using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Partners;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Security;
using AllInsurance.Desktop.Settings;
using AllInsurance.Infrastructure;
using AllInsurance.Desktop.ViewModels;
using AllInsurance.Desktop.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Globalization;

namespace AllInsurance.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private IHost? _host;
    private MainWindow? _mainWindow;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("The application services have not been initialized.");

    public static NavigationService Navigation => Services.GetRequiredService<NavigationService>();

    public App()
    {
        var greekCulture = CultureInfo.GetCultureInfo("el-GR");
        CultureInfo.DefaultThreadCurrentCulture = greekCulture;
        CultureInfo.DefaultThreadCurrentUICulture = greekCulture;
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            Services.GetRequiredService<ILogger<App>>().LogError(args.Exception, "An unhandled UI error occurred.");
        };
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AllInsurance",
            "Data",
            "allinsurance.db");
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureLogging(logging => logging.AddProvider(new LocalErrorLoggerProvider()))
            .ConfigureServices((context, services) =>
            {
                services.AddAllInsuranceInfrastructure(databasePath);
                var authenticationPolicy = new AuthenticationPolicyOptions
                {
                    MaximumFailedAttempts = ReadPolicyValue(
                        context.Configuration["AuthenticationPolicy:MaximumFailedAttempts"],
                        5,
                        "MaximumFailedAttempts"),
                    LockoutDurationMinutes = ReadPolicyValue(
                        context.Configuration["AuthenticationPolicy:LockoutDurationMinutes"],
                        15,
                        "LockoutDurationMinutes")
                };
                _ = authenticationPolicy.GetLockoutDuration();
                services.AddSingleton(authenticationPolicy);
                services.AddSingleton<SessionService>();
                services.AddSingleton<ISessionManager>(provider => provider.GetRequiredService<SessionService>());
                services.AddSingleton<ISessionService>(provider => provider.GetRequiredService<SessionService>());
                services.AddSingleton<RecoveryCodeDisplayState>();
                services.AddSingleton<WorkspacePreferences>();
                services.AddSingleton<NavigationService>();
                services.AddTransient<IAdministratorSetupService, AdministratorSetupService>();
                services.AddTransient<IAuthenticationService, AuthenticationService>();
                services.AddTransient<IRecoveryService, RecoveryService>();
                services.AddTransient<IUserAdministrationService, UserAdministrationService>();
                services.AddTransient<ICustomerService, CustomerService>();
                services.AddTransient<ITransactionSettingsService, TransactionSettingsService>();
                services.AddTransient<IBusinessRecordService, BusinessRecordService>();
                services.AddTransient<IPartnerService, PartnerService>();
                services.AddTransient<AllInsurance.Application.Commissions.ICommissionRuleService,
                    AllInsurance.Application.Commissions.CommissionRuleService>();
                services.AddTransient<IFilterOptionService, FilterOptionService>();
                services.AddTransient<AdministratorSetupViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<ForgotPasswordViewModel>();
                services.AddTransient<ForcedPasswordChangeViewModel>();
                services.AddTransient<RecoveryCodeViewModel>();
            })
            .Build();
        await _host.StartAsync();

        UpdateEyeComfortPalette(Services.GetRequiredService<WorkspacePreferences>().EyeComfortEnabled);
        _mainWindow = new MainWindow();
        _mainWindow.ApplyTheme(Services.GetRequiredService<WorkspacePreferences>().Theme);
        _mainWindow.Closed += async (_, _) =>
        {
            Services.GetRequiredService<ISessionService>().EndSession();
            await _host.StopAsync();
            _host.Dispose();
        };
        _mainWindow.Activate();

        try
        {
            var state = await Task.Run(() =>
                Services.GetRequiredService<IStartupInitializer>().Initialize());
            Navigation.NavigateTo(state switch
            {
                StartupState.AdministratorSetupRequired => AppRoute.AdministratorSetup,
                StartupState.LoginRequired => AppRoute.Login,
                StartupState.RepairRequired => AppRoute.RepairRequired,
                _ => throw new InvalidOperationException("The database returned an unknown startup state.")
            });
            _mainWindow.HideStartupProgress();
            var updatedFrom = Environment.GetCommandLineArgs()
                .FirstOrDefault(value => value.StartsWith("--updated-from=", StringComparison.Ordinal));
            if (updatedFrom is not null)
            {
                var oldVersion = updatedFrom["--updated-from=".Length..];
                _mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(450);
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        XamlRoot = _mainWindow.NavigationRoot,
                        Title = "Η αναβάθμιση ολοκληρώθηκε",
                        Content = $"Η εφαρμογή ενημερώθηκε από την έκδοση {oldVersion} στην έκδοση {Updates.ApplicationUpdater.CurrentVersion}. Τα τοπικά δεδομένα σας διατηρήθηκαν.",
                        CloseButtonText = "Εντάξει"
                    };
                    await Views.DialogButtonStyles.ShowAsync(dialog);
                });
            }
        }
        catch (Exception exception) when (
            exception is SqliteException or DbUpdateException or IOException or UnauthorizedAccessException or
                TimeoutException or InvalidOperationException)
        {
            Services.GetRequiredService<ILogger<App>>().LogError(exception, "Application startup failed.");
            Navigation.NavigateTo(AppRoute.StartupFailure);
            _mainWindow.HideStartupProgress();
        }
    }

    public static void ApplyTheme(Microsoft.UI.Xaml.ElementTheme theme) =>
        ((App)Current)._mainWindow?.ApplyTheme(theme);

    private static readonly Dictionary<string, Windows.UI.Color> OriginalLightColors = [];

    public static void UpdateEyeComfortPalette(bool enabled)
    {
        if (Current.Resources.ThemeDictionaries["Light"] is not Microsoft.UI.Xaml.ResourceDictionary light)
            return;
        var palette = new Dictionary<string, string>
        {
            ["PageBackgroundBrush"] = "#FFE4E1D8",
            ["CardBackgroundBrush"] = "#FFF0EDE5",
            ["ChromeBackgroundBrush"] = "#FFDEDCD3",
            ["SidebarBackgroundBrush"] = "#FFD7D5CB",
            ["TableHeaderBackgroundBrush"] = "#FFDAD8CE",
            ["TableBorderBrush"] = "#FF938F84",
            ["ControlFillColorDefaultBrush"] = "#FFE7E4DA",
            ["ControlFillColorSecondaryBrush"] = "#FFE0DCD1",
            ["ControlFillColorInputActiveBrush"] = "#FFF3EFE5",
            ["TextControlBackground"] = "#FFE7E4DA",
            ["TextControlBackgroundPointerOver"] = "#FFE0DCD1",
            ["TextControlBackgroundFocused"] = "#FFF3EFE5",
            ["ComboBoxBackground"] = "#FFE7E4DA",
            ["ComboBoxBackgroundPointerOver"] = "#FFE0DCD1",
            ["ComboBoxBackgroundUnfocused"] = "#FFE7E4DA",
            ["CalendarDatePickerBackground"] = "#FFE7E4DA",
            ["CalendarDatePickerBackgroundPointerOver"] = "#FFE0DCD1",
            ["CalendarViewBackground"] = "#FFF0EDE5",
            ["DatePickerButtonBackground"] = "#FFE7E4DA",
            ["NavigationViewDefaultPaneBackground"] = "#FFD7D5CB",
            ["NavigationViewExpandedPaneBackground"] = "#FFD7D5CB",
            ["NavigationViewContentBackground"] = "#FFE4E1D8",
            ["FlyoutPresenterBackground"] = "#FFF0EDE5",
            ["MenuFlyoutPresenterBackground"] = "#FFF0EDE5",
            ["ContentDialogBackground"] = "#FFF0EDE5",
            ["ComboBoxPopupBackgroundThemeBrush"] = "#FFF0EDE5"
        };
        foreach (var (key, value) in palette)
        {
            if (light[key] is not Microsoft.UI.Xaml.Media.SolidColorBrush brush) continue;
            if (!OriginalLightColors.ContainsKey(key)) OriginalLightColors[key] = brush.Color;
            brush.Color = enabled ? ParsePaletteColor(value) : OriginalLightColors[key];
        }
    }

    private static Windows.UI.Color ParsePaletteColor(string value) => Windows.UI.Color.FromArgb(
        Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16),
        Convert.ToByte(value.Substring(5, 2), 16), Convert.ToByte(value.Substring(7, 2), 16));

    public static void MaximizeWorkspace() =>
        ((App)Current)._mainWindow?.MaximizeWorkspace();

    public static nint MainWindowHandle() =>
        WinRT.Interop.WindowNative.GetWindowHandle(((App)Current)._mainWindow
            ?? throw new InvalidOperationException("Το κύριο παράθυρο δεν είναι διαθέσιμο."));

    private static int ReadPolicyValue(string? value, int defaultValue, string settingName)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"AuthenticationPolicy:{settingName} must be an integer.");
        }

        return result;
    }
}
