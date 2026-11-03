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
        ToolTipService.SetToolTip(grip, "Σύρετε για αλλαγή ύψους σημειώσεων");
        grip.DragDelta += (_, args) =>
        {
            var height = Math.Clamp(host.Height + args.VerticalChange, 72, 500);
            host.Height = height;
            input.Height = height;
        };
        host.Children.Add(grip);
        return host;
    }
}
