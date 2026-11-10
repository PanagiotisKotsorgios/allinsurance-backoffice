using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AllInsurance.Desktop.Controls;

internal sealed class TablePagination
{
    private readonly ListView _list;
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = new() { Content = "‹ Προηγούμενη", MinHeight = 30,
        Padding = new Thickness(8, 3, 8, 3), FontSize = 12 };
    private readonly Button _next = new() { Content = "Επόμενη ›", MinHeight = 30,
        Padding = new Thickness(8, 3, 8, 3), FontSize = 12 };
    private object[] _items = [];
    private Func<TableExportData>? _exportProvider;
    private int _page = 1;
    private const int Size = 25;

    public int PageOffset => (_page - 1) * Size;
    public IReadOnlyList<object> CapturedItems => _items;
    public event Action? PageRendered;
    public StackPanel Bar { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 7,
        Margin = new Thickness(0, 5, 0, 2) };

    public TablePagination(ListView list)
    {
        _list = list;
        _previous.Click += (_, _) => { _page--; Render(); };
        _next.Click += (_, _) => { _page++; Render(); };
        Bar.Children.Add(_previous);
        Bar.Children.Add(_status);
        Bar.Children.Add(_next);
        TableExportActions.Attach(Bar, _list, () => _exportProvider?.Invoke() ?? DefaultExport());
    }

    public void ConfigureExport(Func<TableExportData> provider) => _exportProvider = provider;

    private TableExportData DefaultExport()
    {
        var rows = _items.Select(item =>
        {
            var content = item is ListViewItem row ? row.Content : item;
            if (content is Grid grid)
                return grid.Children.OfType<FrameworkElement>().OrderBy(element => Grid.GetColumn(element)).Select(CellText).ToArray();
            return new[] { content?.ToString() ?? "" };
        }).ToArray();
        var count = rows.Length == 0 ? 0 : rows.Max(row => row.Length);
        var headers = Enumerable.Range(1, count).Select(index => $"Στήλη {index}").ToArray();
        if (_list.Parent is Panel parent)
        {
            var position = parent.Children.IndexOf(_list);
            if (position > 0 && parent.Children[position - 1] is Border { Child: Grid header })
            {
                var visibleHeaders = header.Children.OfType<FrameworkElement>().OrderBy(element => Grid.GetColumn(element)).Select(CellText).ToArray();
                if (visibleHeaders.Length == count) headers = visibleHeaders;
            }
        }
        return new TableExportData("Δεδομένα πίνακα", headers, rows);
    }

    private static string CellText(UIElement element)
    {
        if (element is TextBlock text) return text.Text;
        if (element is HyperlinkButton link) return link.Content?.ToString() ?? "";
        if (element is ContentControl { Content: string value }) return value;
        if (element is Border border && border.Child is UIElement child) return CellText(child);
        if (element is Panel panel)
            return string.Join(" ", panel.Children.OfType<UIElement>().Select(CellText)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return "";
    }

    public void Capture()
    {
        _items = _list.Items.Cast<object>().ToArray();
        _page = 1;
        Render();
    }

    private void Render()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_items.Length / (double)Size));
        _page = Math.Clamp(_page, 1, pages);
        _list.Items.Clear();
        foreach (var item in _items.Skip(PageOffset).Take(Size)) _list.Items.Add(item);
        _status.Text = $"{_items.Length} εγγραφές · Σελίδα {_page}/{pages}";
        _previous.IsEnabled = _page > 1;
        _next.IsEnabled = _page < pages;
        PageRendered?.Invoke();
    }
}
