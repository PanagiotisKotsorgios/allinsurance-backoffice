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
    private bool _iconOnly;
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

    public bool IconOnly
    {
        get => _iconOnly;
        set { if (_iconOnly == value) return; _iconOnly = value; UpdateVisual(); }
    }

    public ActionButton()
    {
        Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(Label))
                AllInsurance.Desktop.Logging.UiAuditRecorder.Record("BUTTON_CLICK", Label);
        };
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
            "Add" => ("#579473", "plus"),
            "Save" => ("#438665", "save"),
            "Receipt" => ("#4F806F", "receipt"),
            "BulkReceipt" => ("#687887", "receipt"),
            "BulkPayment" => ("#687887", "wallet"),
            "Edit" => ("#517FA4", "pencil"),
            "Columns" => ("#517FA4", "columns-3"),
            "Renewals" => ("#517FA4", "refresh-cw"),
            "Delete" => ("#B56868", "trash"),
            "Deactivate" => ("#AE5555", "x"),
            "Close" => ("#AE5555", "x"),
            "SignOut" => ("#AE5555", "log-out"),
            "Statistics" => ("#7D689B", "chart-no-axes-combined"),
            "Filter" => ("#647486", "funnel-x"),
            "Clear" => ("#B56868", "funnel-x"),
            "Choice" => ("#5F7184", "list-filter"),
            "Previous" => ("#647486", "chevron-left"),
            "Next" => ("#647486", "chevron-right"),
            "Upload" => ("#517FA4", "upload"),
            "Open" => ("#517FA4", "external-link"),
            "Payment" => ("#4F806F", "wallet"),
            "CreditTransfer" => ("#4F806F", "refresh-cw"),
            "Users" => ("#517FA4", "users-round"),
            "Security" => ("#7D689B", "shield-check"),
            "Reset" => ("#996C4C", "key-round"),
            "File" => ("#517FA4", "file-text"),
            _ => ("#455A73", "eye")
        };
        Background = new SolidColorBrush(ParseColor(color));
        Foreground = new SolidColorBrush(Colors.White);
        if (_iconOnly)
        {
            MinHeight = 28;
            MinWidth = 34;
            Padding = new Thickness(6, 3, 6, 3);
        }
        else
        {
            ClearValue(MinHeightProperty);
            ClearValue(PaddingProperty);
            ClearValue(MinWidthProperty);
        }
        var image = new Image
        {
            Source = new SvgImageSource(new Uri($"ms-appx:///Assets/Icons/action-{icon}.svg")),
            Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center
        };
        if (_iconOnly)
        {
            Content = image;
            AutomationProperties.SetName(this, Label);
            ToolTipService.SetToolTip(this, Label);
            return;
        }
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(image);
        var label = new TextBlock
        {
            Text = Label, Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(label);
        if (this is not MultiSelectFilter)
        {
            label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            MinWidth = Math.Ceiling(label.DesiredSize.Width + 44);
        }
        Content = content;
        AutomationProperties.SetName(this, Label);
        ToolTipService.SetToolTip(this, Label);
    }

    private static Color ParseColor(string value) => Color.FromArgb(
        255,
        Convert.ToByte(value.Substring(1, 2), 16),
        Convert.ToByte(value.Substring(3, 2), 16),
        Convert.ToByte(value.Substring(5, 2), 16));
}
