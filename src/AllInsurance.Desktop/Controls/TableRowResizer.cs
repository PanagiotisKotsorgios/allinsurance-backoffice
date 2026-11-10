using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.IO;
using System.Text.Json;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class TableRowResizer
{
    private sealed class RowResizeState
    {
        public required ListViewItem Item { get; set; }
        public required string Table { get; set; }
        public required Guid Id { get; set; }
    }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "table-row-heights.json");
    private static readonly Dictionary<string, double> Heights = Load();

    public static double HeightFor(string table, Guid id) =>
        Heights.TryGetValue($"{table}:{id:N}", out var height) ? height : 34;

    public static void Attach(Grid row, ListViewItem item, string table, Guid id)
    {
        row.Height = item.Height = HeightFor(table, id);
        if (row.Tag is RowResizeState existing)
        {
            existing.Item = item;
            existing.Table = table;
            existing.Id = id;
            return;
        }
        var state = new RowResizeState { Item = item, Table = table, Id = id };
        row.Tag = state;
        var grip = new Border { Height = 7, VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Colors.Transparent) };
        grip.Child = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Color.FromArgb(255, 155, 169, 183)) };
        Grid.SetColumnSpan(grip, row.ColumnDefinitions.Count);
        row.Children.Add(grip);
        var dragging = false;
        var startY = 0d;
        var startHeight = 34d;
        void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            Heights[$"{state.Table}:{state.Id:N}"] = row.Height;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Heights));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        grip.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            startY = args.GetCurrentPoint(row).Position.Y;
            startHeight = row.Height;
            dragging = grip.CapturePointer(args.Pointer);
            args.Handled = true;
        };
        grip.PointerMoved += (_, args) =>
        {
            if (!dragging) return;
            row.Height = state.Item.Height = Math.Clamp(startHeight +
                args.GetCurrentPoint(row).Position.Y - startY, 30, 160);
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

    private static Dictionary<string, double> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return (JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(FilePath)) ?? [])
                    .Where(item => double.IsFinite(item.Value) && item.Value is >= 30 and <= 160)
                    .ToDictionary(item => item.Key, item => item.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return [];
    }
}
