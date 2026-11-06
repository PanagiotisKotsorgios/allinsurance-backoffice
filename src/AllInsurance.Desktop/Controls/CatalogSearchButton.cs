using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AllInsurance.Desktop.Controls;

internal static class CatalogSearchButton
{
    public static Button Create(ComboBox target, string label)
    {
        var search = new AutoSuggestBox { PlaceholderText = "Αναζήτηση " + label,
            MinHeight = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        string[] Choices() => (target.ItemsSource as System.Collections.IEnumerable)?.Cast<object>()
            .OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item)).ToArray() ?? [];
        void Refresh(string query)
        {
            search.ItemsSource = Choices()
                .Where(item => query.Length == 0 || item.Contains(query,
                    StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(item => item.StartsWith(query,
                    StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
                .ThenBy(item => item, StringComparer.CurrentCultureIgnoreCase)
                .Take(100).ToArray();
            search.IsSuggestionListOpen = search.ItemsSource is string[] { Length: > 0 };
        }
        search.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                Refresh(search.Text.Trim());
        };
        search.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is string value) target.SelectedItem = value;
        };
        search.QuerySubmitted += (_, args) =>
        {
            if (args.ChosenSuggestion is string value) target.SelectedItem = value;
            else if (search.ItemsSource is string[] { Length: 1 } only)
                target.SelectedItem = only[0];
        };
        search.GotFocus += (_, _) => Refresh("");
        var button = new Button { Width = 38, Height = 38, MinHeight = 38,
            Padding = new Thickness(0), Content = new FontIcon { Glyph = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16 } };
        ToolTipService.SetToolTip(button, "Αναζήτηση " + label);
        button.Flyout = new Flyout { Content = new StackPanel { Width = 320,
            Children = { search } } };
        return button;
    }
}
