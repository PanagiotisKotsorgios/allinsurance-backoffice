using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class SectionPage : Page
{
    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    public SectionPage(WorkspaceSection section)
    {
        InitializeComponent();
        var accent = Color.FromArgb(
            255,
            Convert.ToByte(section.Accent.Substring(1, 2), 16),
            Convert.ToByte(section.Accent.Substring(3, 2), 16),
            Convert.ToByte(section.Accent.Substring(5, 2), 16));
        SectionIcon.Source = new BitmapImage(new Uri(section.Icon));
        SectionTitle.Text = section.Title;
        SectionDescription.Text = section.Description;
        SearchInput.PlaceholderText = section.SearchHint;
        IconBackground.Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B));

        foreach (var column in section.Columns)
        {
            ColumnHeaders.Children.Add(new TextBlock
            {
                Text = column,
                Width = 155,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accent)
            });
        }

        foreach (var highlight in section.Highlights)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock { Text = highlight, TextWrapping = TextWrapping.Wrap });
            HighlightsPanel.Children.Add(row);
        }
    }
}
