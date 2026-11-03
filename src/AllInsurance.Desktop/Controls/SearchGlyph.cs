using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

public sealed class SearchGlyph : UserControl
{
    public SearchGlyph()
    {
        var stroke = new SolidColorBrush(Color.FromArgb(255, 158, 177, 195));
        var drawing = new Grid { Width = 24, Height = 24, IsHitTestVisible = false };
        drawing.Children.Add(new Ellipse
        {
            Width = 13, Height = 13, Stroke = stroke, StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(3, 3, 0, 0)
        });
        drawing.Children.Add(new Line
        {
            X1 = 15, Y1 = 15, X2 = 21, Y2 = 21,
            Stroke = stroke, StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        Content = drawing;
        IsHitTestVisible = false;
    }
}
