using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using Windows.UI;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace AllInsurance.Desktop.Controls;

internal static class TableRowStriping
{
    private static readonly ConditionalWeakTable<ListView, object> Attached = new();
    private static readonly ConditionalWeakTable<ListViewItem, RowState> States = new();
    private static readonly SolidColorBrush LightAlternate = new(Color.FromArgb(255, 222, 231, 239));
    private static readonly SolidColorBrush LightBase = new(Color.FromArgb(255, 235, 241, 246));
    private static readonly SolidColorBrush DarkAlternate = new(Color.FromArgb(255, 52, 59, 68));
    private static readonly SolidColorBrush DarkBase = new(Color.FromArgb(255, 43, 49, 57));
    private static readonly SolidColorBrush WarmAlternate = new(Color.FromArgb(255, 219, 216, 205));
    private static readonly SolidColorBrush WarmBase = new(Color.FromArgb(255, 235, 232, 222));
    private static readonly SolidColorBrush LightText = new(Color.FromArgb(255, 30, 43, 54));
    private static readonly SolidColorBrush DarkText = new(Color.FromArgb(255, 244, 247, 250));
    private static readonly SolidColorBrush SelectedRow = new(Color.FromArgb(255, 55, 75, 96));
    private static readonly SolidColorBrush SelectedText = new(Microsoft.UI.Colors.White);

    public static void Attach(ListView list)
    {
        if (Attached.TryGetValue(list, out _)) return;
        Attached.Add(list, new object());
        list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        list.ContainerContentChanging += (_, args) =>
        {
            if (args.ItemContainer is ListViewItem item) Apply(item, args.ItemIndex, list, false);
        };
        list.Loaded += (_, _) =>
        {
            Refresh(list, true);
            // DataTemplate roots can be created after the first Loaded notification.
            list.DispatcherQueue.TryEnqueue(() => Refresh(list, true));
        };
        list.ActualThemeChanged += (_, _) => Refresh(list, true);
        list.SelectionChanged += (_, _) =>
            list.DispatcherQueue.TryEnqueue(() => Refresh(list, true));
        Refresh(list);
    }

    public static void Refresh(ListView list, bool force = false)
    {
        for (var index = 0; index < list.Items.Count; index++)
        {
            var item = list.Items[index] as ListViewItem ?? list.ContainerFromIndex(index) as ListViewItem;
            if (item is not null) Apply(item, index, list, force);
        }
    }

    private static void Apply(ListViewItem item, int index, ListView list, bool force)
    {
        var alternate = index % 2 == 1;
        var state = States.GetValue(item, _ => new RowState());
        item.MinHeight = 0;
        item.Padding = new Thickness(0);
        item.Margin = new Thickness(0);
        if (!state.LoadHooked)
        {
            state.LoadHooked = true;
            item.Loaded += (_, _) =>
            {
                var position = list.Items.IndexOf(item);
                if (position >= 0) Apply(item, position, list, true);
            };
        }
        var selected = item.IsSelected;
        var brush = selected ? SelectedRow : RowBrush(list, alternate);
        if (!ReferenceEquals(item.Background, brush)) item.Background = brush;
        // WinUI visual states may replace the container background before the first hover.
        // Paint the row content itself so its stripe is visible on initial render.
        if (item.Content is Panel content && !ReferenceEquals(content.Background, brush))
            content.Background = brush;
        if (item.ContentTemplateRoot is Panel template && !ReferenceEquals(template.Background, brush))
            template.Background = brush;
        // Rows supplied by the card views can keep a white foreground after a theme change
        // or after WinUI recycles a container. Keep uncolored cells readable on every stripe.
        if (item.Content is Panel contentRow) ApplyPlainCellText(contentRow, list.ActualTheme, selected);
        if (item.ContentTemplateRoot is Panel templateRow && !ReferenceEquals(templateRow, item.Content))
            ApplyPlainCellText(templateRow, list.ActualTheme, selected);
    }

    private static void ApplyPlainCellText(Panel row, ElementTheme theme, bool selected)
    {
        var foreground = selected ? SelectedText : theme == ElementTheme.Dark ? DarkText : LightText;
        foreach (var child in row.Children)
        {
            if (child is not Border { Background: null, Child: TextBlock label }) continue;
            if (label.Foreground is null || ReferenceEquals(label.Foreground, LightText) ||
                ReferenceEquals(label.Foreground, DarkText) || ReferenceEquals(label.Foreground, SelectedText) ||
                label.Foreground is SolidColorBrush { Color: var color } &&
                (color == Microsoft.UI.Colors.White && theme != ElementTheme.Dark ||
                 color == Microsoft.UI.Colors.Black && theme == ElementTheme.Dark))
                label.Foreground = foreground;
        }
    }

    public static void ApplyRow(Panel row, int index, ListView list)
    {
        if (index < 0) return;
        var brush = RowBrush(list, index % 2 == 1);
        if (!ReferenceEquals(row.Background, brush)) row.Background = brush;
    }

    private static SolidColorBrush RowBrush(ListView list, bool alternate)
    {
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        var theme = preferences.Theme == ElementTheme.Default ? list.ActualTheme : preferences.Theme;
        return theme == ElementTheme.Dark ? alternate ? DarkAlternate : DarkBase
            : preferences.EyeComfortEnabled ? alternate ? WarmAlternate : WarmBase
            : alternate ? LightAlternate : LightBase;
    }

    private sealed class RowState
    {
        public bool LoadHooked { get; set; }
    }
}
