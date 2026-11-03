using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class SearchFieldVisual
{
    public static Border Wrap(TextBox search)
    {
        var margin = search.Margin;
        search.Margin = new Thickness(0);
        search.Height = Math.Max(42, search.Height + 2);
        search.MinHeight = search.Height;
        search.Padding = new Thickness(42, 0, 12, 0);
        search.CornerRadius = new CornerRadius(7);
        search.BorderThickness = new Thickness(0);
        search.HorizontalAlignment = HorizontalAlignment.Stretch;
        search.VerticalContentAlignment = VerticalAlignment.Center;
        var host = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        host.Children.Add(search);
        host.Children.Add(new FontIcon
        {
            Glyph = "\uE721", FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 20, Width = 24, Height = 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsHitTestVisible = false
        });
        return new Border
        {
            Child = host, Height = search.Height + 4, Margin = margin,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 124, 145, 167)),
            BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(9),
            Padding = new Thickness(1)
        };
    }
}
