using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

public sealed class VisibleSelectionCheckBox : ToggleButton
{
    private readonly Border _square = new()
    {
        Width = 22,
        Height = 22,
        CornerRadius = new CornerRadius(4),
        BorderThickness = new Thickness(2),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _check = new()
    {
        Text = "✓",
        FontSize = 17,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    public VisibleSelectionCheckBox()
    {
        Width = 32;
        Height = 32;
        MinWidth = 32;
        MinHeight = 32;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        UseSystemFocusVisuals = false;
        _square.Child = _check;
        Content = _square;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, "Επιλογή εγγραφής");
        Checked += (_, _) => UpdateVisual();
        Unchecked += (_, _) => UpdateVisual();
        ActualThemeChanged += (_, _) => UpdateVisual();
        Loaded += (_, _) => UpdateVisual();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        var selected = IsChecked == true;
        var dark = ActualTheme == ElementTheme.Dark;
        _square.BorderBrush = new SolidColorBrush(dark
            ? selected ? Color.FromArgb(255, 205, 218, 226) : Color.FromArgb(255, 188, 202, 213)
            : selected ? Color.FromArgb(255, 45, 70, 91) : Color.FromArgb(255, 58, 86, 110));
        _square.Background = selected
            ? new SolidColorBrush(dark ? Color.FromArgb(255, 88, 108, 124) : Color.FromArgb(255, 62, 88, 110))
            : new SolidColorBrush(dark ? Color.FromArgb(255, 42, 51, 61) : Color.FromArgb(255, 255, 255, 255));
        _check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }
}
