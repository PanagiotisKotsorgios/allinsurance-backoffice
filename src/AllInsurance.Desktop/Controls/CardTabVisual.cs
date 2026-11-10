using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class CardTabVisual
{
    public static (Button Button, TextBlock Label) Create(string title, string icon, bool dark)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image { Source = new SvgImageSource(new Uri(
            $"ms-appx:///Assets/Icons/tab-{icon}.svg")), Width = 16, Height = 16 });
        var label = new TextBlock { Text = title, FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(label);
        var surface = new Border { Child = content, MinHeight = 34,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(dark
                ? Color.FromArgb(255, 99, 106, 115) : Color.FromArgb(255, 145, 153, 162)) };
        var button = new Button { Content = surface, MinHeight = 34, MinWidth = 0,
            Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 0),
            BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent) };
        label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        button.MinWidth = Math.Max(84, Math.Ceiling(label.DesiredSize.Width + 16 + 5 + 20));
        ButtonMotion.Attach(button);
        SetSelected(button, label, false, dark);
        return (button, label);
    }

    public static void SetSelected(Button button, TextBlock label, bool selected, bool dark)
    {
        if (button.Content is Border surface)
            surface.Background = new SolidColorBrush(dark
                ? selected ? Color.FromArgb(255, 100, 107, 116) : Color.FromArgb(255, 64, 70, 78)
                : selected ? Color.FromArgb(255, 93, 104, 117) : Color.FromArgb(255, 202, 208, 215));
        label.Foreground = new SolidColorBrush(dark
            ? selected ? Colors.White : Color.FromArgb(255, 226, 231, 236)
            : selected ? Colors.White : Color.FromArgb(255, 44, 52, 62));
    }

    public static void Layout(Grid strip, double availableWidth)
    {
        var buttons = strip.Children.OfType<Button>().ToArray();
        if (buttons.Length == 0 || availableWidth <= 0) return;
        var width = Math.Max(84, buttons.Max(button => button.MinWidth) + 2);
        var columns = Math.Min(buttons.Length, Math.Max(1, (int)(availableWidth / width)));
        strip.Width = availableWidth;
        strip.ColumnDefinitions.Clear();
        strip.RowDefinitions.Clear();
        for (var index = 0; index < columns; index++)
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < (buttons.Length + columns - 1) / columns; index++)
            strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(buttons[index], index / columns);
            Grid.SetColumn(buttons[index], index % columns);
        }
    }
}
