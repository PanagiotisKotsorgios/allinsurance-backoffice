using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class TableRowStriping
{
    private static readonly ConditionalWeakTable<ListView, object> Attached = new();
    private static readonly ConditionalWeakTable<ListViewItem, RowState> States = new();
    private static readonly SolidColorBrush LightAlternate = new(Color.FromArgb(255, 222, 231, 239));
    private static readonly SolidColorBrush DarkAlternate = new(Color.FromArgb(255, 52, 59, 68));

    public static void Attach(ListView list)
    {
        if (Attached.TryGetValue(list, out _)) return;
        Attached.Add(list, new object());
        list.ContainerContentChanging += (_, args) =>
        {
            if (args.ItemContainer is ListViewItem item) Apply(item, args.ItemIndex, list.ActualTheme);
        };
        list.Loaded += (_, _) => Refresh(list);
        list.ActualThemeChanged += (_, _) => Refresh(list);
        list.LayoutUpdated += (_, _) =>
        {
            // Directly supplied ListViewItem rows do not always raise ContainerContentChanging.
            if (list.Items.Count <= 250) Refresh(list);
        };
        Refresh(list);
    }

    public static void Refresh(ListView list)
    {
        for (var index = 0; index < list.Items.Count; index++)
        {
            var item = list.Items[index] as ListViewItem ?? list.ContainerFromIndex(index) as ListViewItem;
            if (item is not null) Apply(item, index, list.ActualTheme);
        }
    }

    private static void Apply(ListViewItem item, int index, ElementTheme theme)
    {
        var alternate = index % 2 == 1;
        var dark = theme == ElementTheme.Dark;
        var state = States.GetValue(item, _ => new RowState());
        if (state.Applied && state.Alternate == alternate && state.Dark == dark) return;
        state.Applied = true;
        state.Alternate = alternate;
        state.Dark = dark;
        if (alternate) item.Background = dark ? DarkAlternate : LightAlternate;
        else item.ClearValue(Control.BackgroundProperty);
    }

    private sealed class RowState
    {
        public bool Applied { get; set; }
        public bool Alternate { get; set; }
        public bool Dark { get; set; }
    }
}
