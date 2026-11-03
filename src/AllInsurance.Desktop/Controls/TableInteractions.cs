using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace AllInsurance.Desktop.Controls;

internal static class TableInteractions
{
    public static Action Attach(Grid header, ListView list)
    {
        var count = header.ColumnDefinitions.Count;
        var order = Enumerable.Range(0, count).ToArray();
        var widths = header.ColumnDefinitions.Select(column => column.Width).ToArray();
        var descending = false;
        var sortedColumn = -1;

        void Apply(Grid grid)
        {
            if (grid.ColumnDefinitions.Count != count || grid.Children.Count < count) return;
            for (var position = 0; position < count; position++)
            {
                var original = order[position];
                grid.ColumnDefinitions[position].Width = widths[original];
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
            foreach (var item in list.Items.OfType<ListViewItem>())
                if (item.Content is Grid grid) Apply(grid);
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

        for (var index = 0; index < count; index++)
        {
            var original = index;
            var cell = (FrameworkElement)header.Children[index];
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

        return ApplyAll;
    }

    private static string CellText(ListViewItem item, int original)
    {
        if (item.Content is not Grid grid || original >= grid.Children.Count) return "";
        return grid.Children[original] is Border { Child: TextBlock text } ? text.Text : "";
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
