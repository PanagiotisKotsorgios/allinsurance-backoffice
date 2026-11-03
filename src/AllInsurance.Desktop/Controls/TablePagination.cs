using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Controls;

internal sealed class TablePagination
{
    private readonly ListView _list;
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = new() { Content = "‹ Προηγούμενη" };
    private readonly Button _next = new() { Content = "Επόμενη ›" };
    private object[] _items = [];
    private int _page = 1;
    private const int Size = 25;

    public int PageOffset => (_page - 1) * Size;
    public StackPanel Bar { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 10,
        Margin = new Thickness(0, 8, 0, 4) };

    public TablePagination(ListView list)
    {
        _list = list;
        _previous.Click += (_, _) => { _page--; Render(); };
        _next.Click += (_, _) => { _page++; Render(); };
        Bar.Children.Add(_previous);
        Bar.Children.Add(_status);
        Bar.Children.Add(_next);
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
    }
}
