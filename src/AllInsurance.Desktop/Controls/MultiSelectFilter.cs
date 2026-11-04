using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Controls;

public sealed class MultiSelectFilter : ActionButton
{
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Multiple,
        Height = 260, MinWidth = 350 };
    private readonly TextBox _search = new() { PlaceholderText = "Αναζήτηση επιλογής..." };
    private string[] _options = [];
    private readonly HashSet<string> _selected = new(StringComparer.CurrentCultureIgnoreCase);
    private bool _updating;
    private bool _initialized;
    private bool _selectNewOptions = true;
    private string _title = "Φίλτρο";

    public event EventHandler? SelectionChanged;
    public IReadOnlySet<string> SelectedValues => _selected;

    public string Title
    {
        get => _title;
        set { _title = value; UpdateLabel(); }
    }

    public MultiSelectFilter()
    {
        Kind = "Choice";
        UpdateLabel();
        var all = new ActionButton { Kind = "Add", Label = "Επιλογή όλων", MinWidth = 170 };
        var none = new ActionButton { Kind = "Filter", Label = "Αποεπιλογή όλων", MinWidth = 170 };
        all.Click += (_, _) => SelectAll();
        none.Click += (_, _) => DeselectAll();
        _search.TextChanged += (_, _) => Render();
        _list.SelectionChanged += (_, args) =>
        {
            if (_updating) return;
            foreach (var item in args.AddedItems.OfType<string>()) _selected.Add(item);
            foreach (var item in args.RemovedItems.OfType<string>()) _selected.Remove(item);
            _selectNewOptions = _options.Length > 0 && _selected.Count == _options.Length;
            UpdateLabel();
            AllInsurance.Desktop.Logging.UiAuditRecorder.Record("FILTER_CHANGED", $"{_title}: {_selected.Count}/{_options.Length}");
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(all);
        buttons.Children.Add(none);
        var content = new StackPanel { Spacing = 8, Padding = new Thickness(8) };
        content.Children.Add(_search);
        content.Children.Add(new TextBlock { Text = "Εμφανίζονται έως 100 επιλογές. Πληκτρολογήστε για αναζήτηση." });
        content.Children.Add(buttons);
        content.Children.Add(_list);
        Flyout = new Flyout { Content = content };
    }

    public void SetOptions(IEnumerable<string> options)
    {
        var values = options.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (_options.SequenceEqual(values)) return;
        var wasAll = !_initialized || _selectNewOptions;
        _options = values;
        _selected.IntersectWith(values);
        if (wasAll) _selected.UnionWith(values);
        _initialized = true;
        Render();
        UpdateLabel();
    }

    public void SelectAll() => SetSelection(_options, true);
    public void DeselectAll() => SetSelection([], false);

    private void SetSelection(IEnumerable<string> values, bool selectNewOptions)
    {
        _selectNewOptions = selectNewOptions;
        _selected.Clear();
        _selected.UnionWith(values);
        Render();
        UpdateLabel();
        if (_initialized)
            AllInsurance.Desktop.Logging.UiAuditRecorder.Record("FILTER_CHANGED", $"{_title}: {_selected.Count}/{_options.Length}");
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Render()
    {
        _updating = true;
        try
        {
            var term = _search.Text.Trim();
            var visible = (term.Length == 0 ? _options.AsEnumerable() : _options.Where(value =>
                value.Contains(term, StringComparison.CurrentCultureIgnoreCase))).Take(100).ToArray();
            _list.ItemsSource = visible;
            _list.SelectedItems.Clear();
            foreach (var value in visible.Where(value => _selected.Contains(value)))
                _list.SelectedItems.Add(value);
        }
        finally { _updating = false; }
    }

    private void UpdateLabel() => Label = _selected.Count == _options.Length
        ? $"{_title}: Όλα" : $"{_title}: {_selected.Count}/{_options.Length}";
}
