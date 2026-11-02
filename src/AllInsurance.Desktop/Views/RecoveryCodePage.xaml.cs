using AllInsurance.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class RecoveryCodePage : Page
{
    public RecoveryCodePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<RecoveryCodeViewModel>();
    }
}
