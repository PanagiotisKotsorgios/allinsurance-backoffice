using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AllInsurance.Desktop.Controls;

internal static class RecordHistoryTable
{
    private static readonly string[] Headings = ["Ημερομηνία / ώρα", "Ενέργεια", "Περιγραφή", "Χρήστης"];

    public static Grid Header() => GridFor(Headings, true);

    public static ListViewItem Row(BusinessRecordActivity activity) => new()
    {
        Tag = activity,
        Content = GridFor([
            activity.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            activity.Action, activity.Description ?? "", activity.ActorName ?? ""
        ], false),
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(0), Margin = new Thickness(0)
    };

    private static Grid GridFor(string[] values, bool header)
    {
        var grid = new Grid { Height = header ? 38 : 42,
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(160) },
                new ColumnDefinition { Width = new GridLength(150) },
                new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(160) } } };
        if (header)
            grid.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderBackgroundBrush"];
        for (var index = 0; index < values.Length; index++)
        {
            var cell = new Border { BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
                BorderThickness = new Thickness(0, 0, index == values.Length - 1 ? 0 : 1, 1),
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock { Text = values[index], VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal } };
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
        }
        return grid;
    }
}
