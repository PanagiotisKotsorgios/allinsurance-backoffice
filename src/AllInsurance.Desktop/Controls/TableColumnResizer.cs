using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class TableColumnResizer
{
    public static void Attach(Grid header, Action<int, double> resize, Action? completed = null)
    {
        for (var index = 0; index < header.Children.Count; index++)
        {
            if (header.Children[index] is not Border cell || cell.Child is null) continue;
            var original = index;
            var caption = cell.Child;
            cell.Child = null;
            var host = new Grid();
            host.Children.Add(caption);
            var grip = new Border { Width = 12, HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Colors.Transparent) };
            grip.Child = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(255, 115, 135, 154)) };
            host.Children.Add(grip);
            cell.Child = host;
            var dragging = false;
            var startX = 0d;
            var startWidth = 0d;
            void EndDrag()
            {
                if (!dragging) return;
                dragging = false;
                completed?.Invoke();
            }
            grip.PointerPressed += (_, args) =>
            {
                if (!args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
                startX = args.GetCurrentPoint(header).Position.X;
                startWidth = header.ColumnDefinitions[Grid.GetColumn(cell)].ActualWidth;
                dragging = grip.CapturePointer(args.Pointer);
                args.Handled = true;
            };
            grip.PointerMoved += (_, args) =>
            {
                if (!dragging) return;
                var width = Math.Clamp(startWidth + args.GetCurrentPoint(header).Position.X - startX,
                    70, 520);
                resize(original, width);
                args.Handled = true;
            };
            grip.PointerReleased += (_, args) =>
            {
                if (!dragging) return;
                grip.ReleasePointerCapture(args.Pointer);
                EndDrag();
                args.Handled = true;
            };
            grip.PointerCaptureLost += (_, _) => EndDrag();
            grip.Tapped += (_, args) => args.Handled = true;
            grip.DoubleTapped += (_, args) => args.Handled = true;
        }
    }
}
