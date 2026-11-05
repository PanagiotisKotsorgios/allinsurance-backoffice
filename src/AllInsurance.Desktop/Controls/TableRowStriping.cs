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

    public static void Attach(ListView list)
    {
        if (Attached.TryGetValue(list, out _)) return;
        Attached.Add(list, new object());
        list.ContainerContentChanging += (_, args) =>
        {
            if (args.ItemContainer is ListViewItem item) Apply(item, args.ItemIndex, list, false);
        };
        list.Loaded += (_, _) => Refresh(list, true);
        list.ActualThemeChanged += (_, _) => Refresh(list, true);
        list.LayoutUpdated += (_, _) =>
        {
            // Directly supplied ListViewItem rows do not always raise ContainerContentChanging.
            if (list.Items.Count <= 250) Refresh(list);
        };
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
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        var theme = preferences.Theme == ElementTheme.Default ? list.ActualTheme : preferences.Theme;
        var dark = theme == ElementTheme.Dark;
        var warm = !dark && preferences.EyeComfortEnabled;
        var state = States.GetValue(item, _ => new RowState());
        if (!state.LoadHooked)
        {
            state.LoadHooked = true;
            item.Loaded += (_, _) => Refresh(list, true);
        }
        if (!force && state.Applied && state.Alternate == alternate && state.Dark == dark && state.Warm == warm) return;
        state.Applied = true;
        state.Alternate = alternate;
        state.Dark = dark;
        state.Warm = warm;
        item.Background = dark ? alternate ? DarkAlternate : DarkBase
            : warm ? alternate ? WarmAlternate : WarmBase
            : alternate ? LightAlternate : LightBase;
    }

    private sealed class RowState
    {
        public bool Applied { get; set; }
        public bool LoadHooked { get; set; }
        public bool Alternate { get; set; }
        public bool Dark { get; set; }
        public bool Warm { get; set; }
    }
}
