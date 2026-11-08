using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

/// <summary>Compact card code, shared by record cards.</summary>
public sealed class CardHeaderCode : StackPanel
{
    private readonly TextBlock _label = new() { Foreground = new SolidColorBrush(Colors.White),
        VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _copy = new() { Background = new SolidColorBrush(Colors.Transparent),
        BorderThickness = new Thickness(0), Padding = new Thickness(6, 4, 6, 4),
        VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _input = new() { Width = 170, Height = 34, MinHeight = 34,
        MaxLength = 40, PlaceholderText = "Κωδικός", VerticalContentAlignment = VerticalAlignment.Center };
    private string _code = "";

    public CardHeaderCode()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        var display = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        display.Children.Add(_label);
        display.Children.Add(new FontIcon { Glyph = "\uE8C8", FontSize = 14,
            Foreground = new SolidColorBrush(Colors.White) });
        _copy.Content = display;
        _copy.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_code)) return;
            var data = new DataPackage();
            data.SetText(_code);
            Clipboard.SetContent(data);
            var flyout = new Flyout { Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 66, 130, 91)),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 6, 10, 6),
                Child = new TextBlock { Text = "Ο κωδικός αντιγράφηκε",
                    Foreground = new SolidColorBrush(Colors.White) }
            } };
            flyout.ShowAt(_copy);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (_, _) => { timer.Stop(); flyout.Hide(); };
            timer.Start();
        };
        ToolTipService.SetToolTip(_copy, "Αντιγραφή κωδικού");
        Children.Add(_copy);
        Children.Add(_input);
        SetCode(null);
    }

    public string EnteredCode => _input.Text.Trim();

    public void SetCode(string? code)
    {
        _code = code?.Trim() ?? "";
        _label.Text = "ΚΩΔΙΚΟΣ  " + (string.IsNullOrEmpty(_code) ? "Νέο" : _code);
        _input.Text = _code;
    }

    public void SetEditing(bool editing)
    {
        _copy.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        _input.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }
}
