using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

/// <summary>A compact date field with both keyboard entry and a calendar flyout.</summary>
public sealed class EditableDateInput : UserControl
{
    private static readonly string[] Formats = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"];
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");
    private readonly TextBox _text = new()
    {
        PlaceholderText = "ηη/μμ/εεεε", VerticalContentAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly CalendarView _calendar = new()
    {
        SelectionMode = CalendarViewSelectionMode.Single,
        MinDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
        MaxDate = new DateTimeOffset(2100, 12, 31, 0, 0, 0, TimeSpan.Zero)
    };
    private readonly Flyout _flyout = new();
    private readonly Grid _layout = new();
    private DateTimeOffset? _date;
    private bool _updating;
    private string _previousText = "";

    public event EventHandler? DateChanged;

    public string PlaceholderText
    {
        get => _text.PlaceholderText;
        set => _text.PlaceholderText = value;
    }

    public DateTimeOffset? Date
    {
        get => _date;
        set => SetDate(value, normalizeText: true);
    }

    public EditableDateInput()
    {
        MinHeight = 36;
        _layout.ColumnDefinitions.Add(new ColumnDefinition());
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        _layout.Children.Add(_text);
        _flyout.Content = _calendar;
        var open = new Button
        {
            Content = new SymbolIcon(Symbol.Calendar), Flyout = _flyout,
            Width = 34, Height = 34, Padding = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(open, "Επιλογή ημερομηνίας από ημερολόγιο");
        Grid.SetColumn(open, 1);
        _layout.Children.Add(open);
        Content = _layout;
        _text.TextChanged += (_, _) =>
        {
            if (_updating) return;
            InsertDateSeparators();
            _previousText = _text.Text;
            if (TryParse(_text.Text, out var date)) SetDate(date, normalizeText: false);
            else SetDate(null, normalizeText: false);
        };
        _text.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_text.Text)) return;
            if (TryParse(_text.Text, out var date)) SetDate(date, normalizeText: true);
            else
            {
                _text.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                ToolTipService.SetToolTip(_text, "Χρησιμοποιήστε ημερομηνία της μορφής ηη/μμ/εεεε.");
            }
        };
        _calendar.SelectedDatesChanged += (_, args) =>
        {
            if (_updating || args.AddedDates.Count == 0) return;
            SetDate(args.AddedDates[0], normalizeText: true);
            _flyout.Hide();
        };
    }

    public DateTime? GetValidatedDate(string label)
    {
        if (string.IsNullOrWhiteSpace(_text.Text)) return null;
        if (!TryParse(_text.Text, out var date))
            throw new ArgumentException($"Το πεδίο «{label}» χρειάζεται έγκυρη ημερομηνία ηη/μμ/εεεε.");
        return date.Date;
    }

    private void SetDate(DateTimeOffset? value, bool normalizeText)
    {
        var changed = _date?.Date != value?.Date;
        _date = value;
        _updating = true;
        try
        {
            if (normalizeText) _text.Text = value?.ToString("dd/MM/yyyy", Greek) ?? "";
            _previousText = _text.Text;
            _calendar.SelectedDates.Clear();
            if (value is DateTimeOffset selected &&
                selected >= _calendar.MinDate && selected <= _calendar.MaxDate)
            {
                _calendar.SelectedDates.Add(selected);
                _calendar.SetDisplayDate(selected);
            }
            _text.ClearValue(TextBox.BorderBrushProperty);
            ToolTipService.SetToolTip(_text, null);
        }
        finally { _updating = false; }
        if (changed) DateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InsertDateSeparators()
    {
        var input = _text.Text;
        if (input.Length == 0 || input.Contains('-') ||
            input.Any(character => !char.IsDigit(character) && character != '/')) return;

        var digits = new string(input.Where(char.IsDigit).Take(8).ToArray());
        var growing = input.Length > _previousText.Length;
        string formatted;
        if (digits.Length < 2) formatted = input;
        else if (digits.Length == 2) formatted = digits + (growing || input.EndsWith('/') ? "/" : "");
        else if (digits.Length < 4) formatted = digits[..2] + "/" + digits[2..];
        else if (digits.Length == 4) formatted = digits[..2] + "/" + digits[2..] +
            (growing || input.EndsWith('/') ? "/" : "");
        else formatted = digits[..2] + "/" + digits[2..4] + "/" + digits[4..];

        if (formatted == input) return;
        var caret = _text.SelectionStart;
        _updating = true;
        try
        {
            _text.Text = formatted;
            _text.SelectionStart = Math.Min(formatted.Length,
                caret + (formatted.Length - input.Length));
        }
        finally { _updating = false; }
    }

    private static bool TryParse(string text, out DateTimeOffset date)
    {
        date = default;
        if (!DateTime.TryParseExact(text.Trim(), Formats, Greek, DateTimeStyles.None, out var parsed))
            return false;
        date = new DateTimeOffset(parsed.Date);
        return true;
    }
}
