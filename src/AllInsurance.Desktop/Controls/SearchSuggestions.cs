using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Controls;

internal static class SearchSuggestions
{
    public static void CloseOnLostFocus(AutoSuggestBox box)
    {
        box.LostFocus += (_, _) => box.DispatcherQueue.TryEnqueue(() =>
        {
            if (box.FocusState == FocusState.Unfocused)
                box.IsSuggestionListOpen = false;
        });
    }

    public static string[] FirstHundred(IEnumerable<string> source, string? query)
    {
        var term = query?.Trim() ?? "";
        return source.Where(value => !string.IsNullOrWhiteSpace(value) &&
                (term.Length == 0 || value.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value.StartsWith(term, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .Take(100).ToArray();
    }
}
