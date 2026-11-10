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
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4,
            Margin = new Thickness(7, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image { Source = new SvgImageSource(new Uri(
            $"ms-appx:///Assets/Icons/tab-{icon}.svg")), Width = 16, Height = 16 });
        var label = new TextBlock { Text = title, FontSize = 12.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(label);
        var surface = new Border { Child = content, MinHeight = 36,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(dark
                ? Color.FromArgb(255, 99, 106, 115) : Color.FromArgb(255, 145, 153, 162)) };
        var button = new Button { Content = surface, MinHeight = 36, MinWidth = 0,
            Padding = new Thickness(0), Margin = new Thickness(0),
            BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent) };
        label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        button.MinWidth = Math.Max(80, Math.Ceiling(label.DesiredSize.Width + 16 + 4 + 28));
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
        strip.Width = availableWidth;
        strip.ColumnDefinitions.Clear();
        strip.RowDefinitions.Clear();
        var x = 0d;
        var y = 0d;
        const double rowHeight = 36;
        foreach (var button in buttons)
        {
            // Re-measure after theme and DPI changes; the initial label width can be stale.
            if (button.Content is Border { Child: StackPanel content } &&
                content.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
            {
                label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                var iconWidth = content.Children.OfType<Image>().FirstOrDefault()?.Width ?? 0;
                button.MinWidth = Math.Max(button.MinWidth,
                    Math.Ceiling(label.DesiredSize.Width + iconWidth + content.Spacing + 28));
            }
            var width = Math.Min(availableWidth, button.MinWidth);
            if (x > 0 && x + width > availableWidth)
            {
                x = 0;
                y += rowHeight;
            }
            button.Width = width;
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.VerticalAlignment = VerticalAlignment.Top;
            button.Margin = new Thickness(x, y, 0, 0);
            Grid.SetRow(button, 0);
            Grid.SetColumn(button, 0);
            x += width;
        }
        strip.Height = y + rowHeight;
    }
}
