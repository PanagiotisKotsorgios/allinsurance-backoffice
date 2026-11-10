using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

public sealed class VisibleSelectionCheckBox : Grid
{
    private readonly CheckBox _input = new()
    {
        Width = 30,
        Height = 30,
        Opacity = 0,
        UseSystemFocusVisuals = false,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Border _square = new()
    {
        Width = 24,
        Height = 24,
        CornerRadius = new CornerRadius(5),
        BorderThickness = new Thickness(1.5),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
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

    public event RoutedEventHandler? Checked;
    public event RoutedEventHandler? Unchecked;

    public bool? IsChecked
    {
        get => _input.IsChecked;
        set => _input.IsChecked = value;
    }

    public VisibleSelectionCheckBox()
    {
        Width = 32;
        Height = 32;
        _square.Child = _check;
        Children.Add(_input);
        Children.Add(_square);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_input, "Επιλογή εγγραφής");
        _input.Checked += (_, args) => { UpdateVisual(); Checked?.Invoke(this, args); };
        _input.Unchecked += (_, args) => { UpdateVisual(); Unchecked?.Invoke(this, args); };
        ActualThemeChanged += (_, _) => UpdateVisual();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        var selected = _input.IsChecked == true;
        var dark = ActualTheme == ElementTheme.Dark;
        _square.BorderBrush = new SolidColorBrush(dark
            ? selected ? Color.FromArgb(255, 181, 197, 209) : Color.FromArgb(255, 178, 191, 202)
            : selected ? Color.FromArgb(255, 57, 82, 103) : Color.FromArgb(255, 83, 106, 126));
        _square.Background = selected
            ? new SolidColorBrush(dark ? Color.FromArgb(255, 101, 117, 130) : Color.FromArgb(255, 79, 96, 112))
            : new SolidColorBrush(dark ? Color.FromArgb(255, 63, 72, 82) : Color.FromArgb(255, 250, 252, 254));
        _check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }
}
