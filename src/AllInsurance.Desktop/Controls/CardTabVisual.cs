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
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image { Source = new SvgImageSource(new Uri(
            $"ms-appx:///Assets/Icons/tab-{icon}.svg")), Width = 25, Height = 25 });
        var label = new TextBlock { Text = title, FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(label);
        var button = new Button { Content = content, MinHeight = 54, MinWidth = 155,
            Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = new SolidColorBrush(dark
                ? Color.FromArgb(255, 82, 89, 98) : Color.FromArgb(255, 198, 205, 213)) };
        return (button, label);
    }

    public static void SetSelected(Button button, TextBlock label, bool selected, bool dark)
    {
        button.Background = new SolidColorBrush(dark
            ? selected ? Color.FromArgb(255, 35, 77, 111) : Color.FromArgb(255, 54, 59, 66)
            : selected ? Color.FromArgb(255, 211, 230, 246) : Color.FromArgb(255, 229, 234, 239));
        label.Foreground = new SolidColorBrush(dark
            ? selected ? Colors.White : Color.FromArgb(255, 226, 231, 236)
            : selected ? Color.FromArgb(255, 15, 79, 127) : Color.FromArgb(255, 43, 53, 65));
    }
}
