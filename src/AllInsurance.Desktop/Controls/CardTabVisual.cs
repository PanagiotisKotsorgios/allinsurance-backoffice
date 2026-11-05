using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class CardTabVisual
{
    public static (Button Button, TextBlock Label) Create(string title, string icon, bool dark)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image { Source = new SvgImageSource(new Uri(
            $"ms-appx:///Assets/Icons/tab-{icon}.svg")), Width = 19, Height = 19 });
        var label = new TextBlock { Text = title, FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(label);
        var tabShape = new Polygon { Stretch = Stretch.Fill,
            Points = new PointCollection
            {
                new Point(0, 40), new Point(8, 4), new Point(13, 0),
                new Point(111, 0), new Point(116, 4), new Point(124, 40)
            },
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(dark
                ? Color.FromArgb(255, 99, 106, 115) : Color.FromArgb(255, 145, 153, 162)) };
        var surface = new Grid { MinHeight = 40 };
        surface.Children.Add(tabShape);
        surface.Children.Add(content);
        var button = new Button { Content = surface, MinHeight = 40, MinWidth = 0,
            Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 0),
            BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent) };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        button.MinWidth = Math.Max(124, Math.Ceiling(label.DesiredSize.Width + 19 + 6 + 32));
        ButtonMotion.Attach(button);
        SetSelected(button, label, false, dark);
        return (button, label);
    }

    public static void SetSelected(Button button, TextBlock label, bool selected, bool dark)
    {
        if (button.Content is Grid surface && surface.Children[0] is Polygon shape)
            shape.Fill = new SolidColorBrush(dark
                ? selected ? Color.FromArgb(255, 100, 107, 116) : Color.FromArgb(255, 64, 70, 78)
                : selected ? Color.FromArgb(255, 93, 104, 117) : Color.FromArgb(255, 202, 208, 215));
        label.Foreground = new SolidColorBrush(dark
            ? selected ? Colors.White : Color.FromArgb(255, 226, 231, 236)
            : selected ? Colors.White : Color.FromArgb(255, 44, 52, 62));
    }
}
