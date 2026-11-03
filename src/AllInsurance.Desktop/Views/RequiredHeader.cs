using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

internal static class RequiredHeader
{
    public static UIElement Create(string label)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        header.Children.Add(new TextBlock { Text = label });
        header.Children.Add(new TextBlock { Text = "*", FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)) });
        return header;
    }
}
