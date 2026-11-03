using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

public class ActionButton : Button
{
    private string _kind = "Open";
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ActionButton),
        new PropertyMetadata(""));

    public string Kind
    {
        get => _kind;
        set { _kind = value; UpdateVisual(); }
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public ActionButton()
    {
        Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["ActionButtonStyle"];
        UseSystemFocusVisuals = true;
        RegisterPropertyChangedCallback(LabelProperty, (_, _) => UpdateVisual());
        ButtonMotion.Attach(this);
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        var (color, icon) = _kind switch
        {
            "Add" => ("#16803D", "plus"),
            "Save" => ("#16803D", "save"),
            "Receipt" => ("#047857", "receipt"),
            "Edit" => ("#1D64B3", "pencil"),
            "Delete" => ("#C62828", "trash"),
            "Deactivate" => ("#C62828", "x"),
            "Close" => ("#B42332", "x"),
            "SignOut" => ("#C62828", "log-out"),
            "Statistics" => ("#7041A3", "chart-no-axes-combined"),
            "Filter" => ("#52657D", "funnel-x"),
            "Clear" => ("#C62828", "funnel-x"),
            "Choice" => ("#455A73", "list-filter"),
            "Previous" => ("#52657D", "chevron-left"),
            "Next" => ("#52657D", "chevron-right"),
            "Upload" => ("#1D64B3", "upload"),
            "Open" => ("#1D64B3", "external-link"),
            "Payment" => ("#A34B19", "wallet"),
            "Users" => ("#1D64B3", "users-round"),
            "Security" => ("#7041A3", "shield-check"),
            "Reset" => ("#A34B19", "key-round"),
            "File" => ("#1D64B3", "file-text"),
            _ => ("#455A73", "eye")
        };
        Background = new SolidColorBrush(ParseColor(color));
        Foreground = new SolidColorBrush(Colors.White);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image
        {
            Source = new SvgImageSource(new Uri($"ms-appx:///Assets/Icons/action-{icon}.svg")),
            Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = Label, Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        Content = content;
        AutomationProperties.SetName(this, Label);
    }

    private static Color ParseColor(string value) => Color.FromArgb(
        255,
        Convert.ToByte(value.Substring(1, 2), 16),
        Convert.ToByte(value.Substring(3, 2), 16),
        Convert.ToByte(value.Substring(5, 2), 16));
}
