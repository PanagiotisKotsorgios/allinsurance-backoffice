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
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Border _square = new()
    {
        Width = 24,
        Height = 24,
        CornerRadius = new CornerRadius(5),
        BorderThickness = new Thickness(2),
        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 56, 162, 235)),
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
        Children.Add(_square);
        Children.Add(_input);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_input, "Επιλογή εγγραφής");
        _input.Checked += (_, args) => { UpdateVisual(); Checked?.Invoke(this, args); };
        _input.Unchecked += (_, args) => { UpdateVisual(); Unchecked?.Invoke(this, args); };
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        var selected = _input.IsChecked == true;
        _square.Background = selected
            ? new SolidColorBrush(Color.FromArgb(255, 27, 115, 190))
            : new SolidColorBrush(Color.FromArgb(255, 245, 250, 255));
        _check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }
}
