using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class CardFieldLayout
{
    public static Grid TwoColumns(out StackPanel left, out StackPanel right, double width = 1100,
        bool preserveColumns = false)
    {
        var grid = new Grid { ColumnSpacing = 9, RowSpacing = 4,
            Margin = new Thickness(8, 4, 8, 5),
            MaxWidth = 1100, Width = Math.Max(280, width) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        left = new StackPanel { Spacing = 0 };
        right = new StackPanel { Spacing = 0 };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        var rightPanel = right;
        grid.SizeChanged += (_, args) =>
        {
            var narrow = !preserveColumns && args.NewSize.Width < 760;
            grid.ColumnSpacing = narrow ? 0 : 9;
            Grid.SetColumn(rightPanel, narrow ? 0 : 1);
            Grid.SetRow(rightPanel, narrow ? grid.Children.Count > 2 ? 2 : 1 : 0);
            if (narrow && grid.RowDefinitions.Count == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions.Add(new RowDefinition());
            }
        };
        return grid;
    }

    public static Grid CreateRow(string label, FrameworkElement input)
    {
        var compactCheck = input is CheckBox or Border { Width: 22, Height: 22 };
        var row = new Grid { ColumnSpacing = 6, MinHeight = compactCheck ? 26 : 34 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var caption = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center };
        caption.Children.Add(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });
        caption.Children.Add(new TextBlock { Text = "*", FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)) });
        row.Children.Add(caption);
        Grid.SetColumn(input, 1);
        if (input is Control control)
        {
            control.MinHeight = Math.Max(compactCheck ? 26 : 34, control.MinHeight);
            control.VerticalAlignment = VerticalAlignment.Center;
        }
        else input.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(input);
        SetRequired(row, label.TrimEnd(' ', '*'), label.EndsWith('*'));
        return row;
    }

    public static void SetRequired(Grid row, string label, bool required)
    {
        if (row.Children[0] is not StackPanel caption || caption.Children.Count < 2) return;
        if (caption.Children[0] is TextBlock text) text.Text = label;
        caption.Children[1].Visibility = required ? Visibility.Visible : Visibility.Collapsed;
    }

    public static void SetRequired(StackPanel panel, FrameworkElement input, string label, bool required)
    {
        var row = panel.Children.OfType<Grid>().First(item => item.Children.Contains(input));
        SetRequired(row, label, required);
    }
}
