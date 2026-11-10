using AllInsurance.Desktop.Controls;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow
{
    private CustomerCardWindow? _linkedCustomerCard;
    private PartnerCardWindow? _linkedPartnerCard;

    private void AddCustomerLink(StackPanel panel)
    {
        panel.Children.Add(CardFieldLayout.CreateRow("Σύνδεση", CustomerLinkButton()));
    }

    private Button CustomerLinkButton()
    {
        var button = LinkedCardButton("Άνοιγμα καρτέλας πελάτη");
        button.Click += async (_, _) =>
        {
            if ((_customerChoice?.SelectedItem as Choice)?.Id is not Guid id)
            {
                await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρώτα πελάτη.");
                return;
            }
            if (_linkedCustomerCard is not null) { _linkedCustomerCard.BringToFront(); return; }
            _linkedCustomerCard = new CustomerCardWindow(id);
            _linkedCustomerCard.Closed += (_, _) => { _linkedCustomerCard = null; BringToFront(); };
            _linkedCustomerCard.BringToFront();
        };
        return button;
    }

    private void AddPartnerLink(StackPanel panel)
    {
        panel.Children.Add(CardFieldLayout.CreateRow("Συνεργάτης", PartnerLinkButton()));
    }

    private Button PartnerLinkButton()
    {
        var button = LinkedCardButton("Άνοιγμα καρτέλας συνεργάτη");
        button.Click += async (_, _) =>
        {
            if ((_policyPartnerChoice?.SelectedItem as Choice)?.Id is not Guid id)
            {
                await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρώτα συνεργάτη.");
                return;
            }
            if (_linkedPartnerCard is not null) { _linkedPartnerCard.BringToFront(); return; }
            _linkedPartnerCard = new PartnerCardWindow(id);
            _linkedPartnerCard.Closed += (_, _) => { _linkedPartnerCard = null; BringToFront(); };
            _linkedPartnerCard.BringToFront();
        };
        return button;
    }

    private static Button LinkedCardButton(string description)
    {
        var button = new ActionButton { Kind = "Open", Label = description,
            IconOnly = true, Width = 38, Height = 36 };
        ToolTipService.SetToolTip(button, description);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, description);
        return button;
    }
}
