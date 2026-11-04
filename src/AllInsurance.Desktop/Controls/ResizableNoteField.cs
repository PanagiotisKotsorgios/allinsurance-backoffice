using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AllInsurance.Desktop.Controls;

internal static class ResizableNoteField
{
    public static Grid Wrap(TextBox input)
    {
        var host = new Grid { Height = input.Height, MinHeight = 72,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        input.HorizontalAlignment = HorizontalAlignment.Stretch;
        input.VerticalAlignment = VerticalAlignment.Stretch;
        host.Children.Add(input);
        var grip = new Thumb { Width = 22, Height = 22,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        host.Children.Add(new TextBlock { Text = "◢", FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 5, 3), IsHitTestVisible = false });
        ToolTipService.SetToolTip(grip, "Σύρετε για αλλαγή πλάτους και ύψους σημειώσεων");
        grip.DragDelta += (_, args) =>
        {
            var height = Math.Clamp(host.Height + args.VerticalChange, 72, 500);
            host.Height = height;
            input.Height = height;
            if (Math.Abs(args.HorizontalChange) > 0.5)
            {
                var available = (host.Parent as Grid)?.ActualWidth - 155 ?? host.ActualWidth;
                var maxWidth = Math.Max(250, available);
                var current = double.IsNaN(host.Width) ? host.ActualWidth : host.Width;
                host.Width = Math.Clamp(current + args.HorizontalChange, 250, maxWidth);
                host.HorizontalAlignment = HorizontalAlignment.Left;
            }
        };
        host.Children.Add(grip);
        return host;
    }
}
