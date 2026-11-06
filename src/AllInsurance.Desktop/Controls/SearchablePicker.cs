using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AllInsurance.Desktop.Controls;

internal sealed class SearchablePicker<T> : StackPanel where T : class
{
    private sealed record Option(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly AutoSuggestBox _search;
    private readonly Button _open;
    private readonly Func<T, string> _label;
    private Option[] _options = [];
    private T? _selected;
    private bool _assigning;

    public T? SelectedItem
    {
        get => _selected;
        set
        {
            _assigning = true;
            _selected = value;
            _search.Text = value is null ? "" : _label(value);
            _assigning = false;
        }
    }

    public event EventHandler? SelectionChanged;

    public SearchablePicker(string header, IEnumerable<T> items, Func<T, string> label,
        T? selected = null)
    {
        _label = label;
        Spacing = 4;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Children.Add(new TextBlock { Text = header });
        var box = new Grid { ColumnDefinitions =
        {
            new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }
        } };
        _search = new AutoSuggestBox { PlaceholderText = "Πληκτρολογήστε για αναζήτηση",
            MinHeight = 42, HorizontalAlignment = HorizontalAlignment.Stretch };
        _search.GotFocus += (_, _) => ShowSuggestions(showAll: true);
        _search.TextChanged += (_, args) =>
        {
            if (_assigning || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var matched = _options.FirstOrDefault(option => string.Equals(option.Label,
                _search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
            Assign(matched?.Value);
            ShowSuggestions();
        };
        _search.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is Option option) Select(option.Value);
        };
        _search.QuerySubmitted += (_, args) =>
        {
            if (args.ChosenSuggestion is Option option) Select(option.Value);
            else if (_search.ItemsSource is Option[] { Length: 1 } single) Select(single[0].Value);
        };
        box.Children.Add(_search);
        _open = new Button { Content = "⌄", Width = 36, Height = 40, MinHeight = 40,
            Padding = new Thickness(0), Margin = new Thickness(0, 1, 1, 1),
            VerticalAlignment = VerticalAlignment.Center };
        _open.Click += (_, _) =>
        {
            _search.Focus(FocusState.Programmatic);
            ShowSuggestions(showAll: true);
        };
        Grid.SetColumn(_open, 1);
        box.Children.Add(_open);
        Children.Add(box);
        SetItems(items, selected);
    }

    public void SetItems(IEnumerable<T> items, T? selected = null)
    {
        _options = items.Select(item => new Option(item, _label(item))).ToArray();
        SelectedItem = selected;
        _search.ItemsSource = _options.Take(100).ToArray();
    }

    private void Select(T value)
    {
        SelectedItem = value;
        _search.IsSuggestionListOpen = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Assign(T? value)
    {
        if (ReferenceEquals(_selected, value)) return;
        _selected = value;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowSuggestions(bool showAll = false)
    {
        var query = showAll ? "" : _search.Text.Trim();
        _search.ItemsSource = _options
            .Where(option => query.Length == 0 || option.Label.Contains(query,
                StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(option => option.Label.StartsWith(query,
                StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(100).ToArray();
        _search.IsSuggestionListOpen = _search.ItemsSource is Option[] { Length: > 0 };
    }
}
