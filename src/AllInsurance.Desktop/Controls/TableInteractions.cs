using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace AllInsurance.Desktop.Controls;

internal static class TableInteractions
{
    public static Action Attach(Grid header, ListView list, FrameworkElement? widthHost = null,
        double horizontalInset = 48)
    {
        TableRowStriping.Attach(list);
        var count = header.ColumnDefinitions.Count;
        var order = Enumerable.Range(0, count).ToArray();
        var widths = header.ColumnDefinitions.Select(column => column.Width).ToArray();
        var descending = false;
        var sortedColumn = -1;
        double DisplayWidth() => Math.Max(widths.Sum(width => width.Value),
            (widthHost?.ActualWidth ?? 0) - horizontalInset - 2);

        void Apply(Grid grid)
        {
            if (grid.ColumnDefinitions.Count != count || grid.Children.Count < count) return;
            var baseWidth = widths.Sum(width => width.Value);
            var scale = baseWidth <= 0 ? 1 : DisplayWidth() / baseWidth;
            grid.Width = DisplayWidth();
            for (var position = 0; position < count; position++)
            {
                var original = order[position];
                grid.ColumnDefinitions[position].Width = new GridLength(widths[original].Value * scale);
                var cell = (FrameworkElement)grid.Children[original];
                Grid.SetColumn(cell, position);
                if (cell is Border border)
                    border.BorderThickness = ReferenceEquals(grid, header)
                        ? position == count - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0)
                        : position == count - 1 ? new Thickness(0, 0, 0, 1) :
                            new Thickness(0, 0, 1, 1);
            }
        }

        void ApplyAll()
        {
            Apply(header);
            if (header.Parent is Border frame) frame.Width = header.Width + 2;
            list.Width = header.Width + 2;
            foreach (var item in list.Items.OfType<ListViewItem>())
                if (item.Content is Grid grid)
                {
                    Apply(grid);
                    item.Width = grid.Width;
                }
            TableRowStriping.Refresh(list);
        }

        void Sort(int original, bool reverse)
        {
            sortedColumn = original;
            descending = reverse;
            var rows = list.Items.OfType<ListViewItem>().ToArray();
            Array.Sort(rows, (left, right) => Compare(CellText(left, original), CellText(right, original)) *
                (reverse ? -1 : 1));
            list.Items.Clear();
            foreach (var row in rows) list.Items.Add(row);
            ApplyAll();
        }

        void Move(int original, int offset)
        {
            var position = Array.IndexOf(order, original);
            var target = position + offset;
            if (target < 0 || target >= count) return;
            (order[position], order[target]) = (order[target], order[position]);
            ApplyAll();
        }

        void MoveTo(int original, int target)
        {
            if (original == target || !order.Contains(original) || !order.Contains(target)) return;
            var columns = order.ToList();
            columns.Remove(original);
            columns.Insert(columns.IndexOf(target), original);
            order = [.. columns];
            ApplyAll();
        }

        for (var index = 0; index < count; index++)
        {
            var original = index;
            var cell = (FrameworkElement)header.Children[index];
            cell.CanDrag = true;
            cell.AllowDrop = true;
            cell.DragStarting += (_, args) => args.Data.SetText($"card-column:{original}");
            cell.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            };
            cell.Drop += async (_, args) =>
            {
                var dragged = await args.DataView.GetTextAsync();
                if (dragged.StartsWith("card-column:", StringComparison.Ordinal) &&
                    int.TryParse(dragged["card-column:".Length..], out var source))
                    MoveTo(source, original);
            };
            cell.Tapped += (_, _) => Sort(original, sortedColumn == original && !descending);
            var menu = new MenuFlyout();
            var ascending = new MenuFlyoutItem { Text = "Ταξινόμηση αύξουσα" };
            ascending.Click += (_, _) => Sort(original, false);
            var descendingItem = new MenuFlyoutItem { Text = "Ταξινόμηση φθίνουσα" };
            descendingItem.Click += (_, _) => Sort(original, true);
            var left = new MenuFlyoutItem { Text = "Μεταφορά αριστερά" };
            left.Click += (_, _) => Move(original, -1);
            var right = new MenuFlyoutItem { Text = "Μεταφορά δεξιά" };
            right.Click += (_, _) => Move(original, 1);
            menu.Items.Add(ascending);
            menu.Items.Add(descendingItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(left);
            menu.Items.Add(right);
            cell.ContextFlyout = menu;
        }

        TableColumnResizer.Attach(header, (original, width) =>
        {
            widths[original] = new GridLength(width);
            ApplyAll();
        });
        if (widthHost is not null)
        {
            void OnHostSizeChanged(object sender, SizeChangedEventArgs args) => ApplyAll();
            header.Loaded += (_, _) =>
            {
                widthHost.SizeChanged += OnHostSizeChanged;
                ApplyAll();
            };
            header.Unloaded += (_, _) => widthHost.SizeChanged -= OnHostSizeChanged;
        }
        return ApplyAll;
    }

    private static string CellText(ListViewItem item, int original)
    {
        if (item.Content is not Grid grid || original >= grid.Children.Count) return "";
        return grid.Children[original] switch
        {
            Border { Child: TextBlock text } => text.Text,
            Border { Child: Grid host } => host.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "",
            _ => ""
        };
    }

    private static int Compare(string left, string right)
    {
        var greek = CultureInfo.GetCultureInfo("el-GR");
        if (DateTime.TryParse(left, greek, DateTimeStyles.None, out var leftDate) &&
            DateTime.TryParse(right, greek, DateTimeStyles.None, out var rightDate))
            return leftDate.CompareTo(rightDate);
        if (decimal.TryParse(left.Replace("€", ""), NumberStyles.Number, greek, out var leftAmount) &&
            decimal.TryParse(right.Replace("€", ""), NumberStyles.Number, greek, out var rightAmount))
            return leftAmount.CompareTo(rightAmount);
        return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
    }
}
