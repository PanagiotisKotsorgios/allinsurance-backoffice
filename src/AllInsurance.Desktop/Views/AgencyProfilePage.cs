using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed class AgencyProfilePage : Page
{
    public AgencyProfilePage()
    {
        var profile = AgencyProfileSettings.Load();
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24),
            MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = "Ρυθμίσεις προφίλ πρακτορείου", FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text =
            "Τα στοιχεία χρησιμοποιούνται στις αποδείξεις είσπραξης και στα έγγραφα του γραφείου.",
            TextWrapping = TextWrapping.Wrap });
        TextBox Add(string label, string value)
        {
            var input = new TextBox { Header = label, Text = value, MinHeight = 42,
                MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Stretch };
            panel.Children.Add(input);
            return input;
        }
        var name = Add("Επωνυμία πρακτορείου *", profile.Name);
        var tax = Add("ΑΦΜ *", profile.TaxNumber);
        var address = Add("Διεύθυνση *", profile.Address);
        var postal = Add("Ταχυδρομικός κώδικας", profile.PostalCode);
        var city = Add("Πόλη *", profile.City);
        var phone = Add("Τηλέφωνο", profile.Phone);
        var email = Add("Email", profile.Email);
        var license = Add("Αριθμός άδειας / μητρώου", profile.LicenseNumber);
        var save = new ActionButton { Kind = "Save", Label = "Αποθήκευση προφίλ",
            HorizontalAlignment = HorizontalAlignment.Left };
        save.IsEnabled = App.Services.GetRequiredService<ISessionService>().Current?
            .Permissions.Contains(Permission.ManageUsers) == true;
        save.Click += async (_, _) =>
        {
            try
            {
                profile.Name = name.Text.Trim();
                profile.TaxNumber = tax.Text.Trim();
                profile.Address = address.Text.Trim();
                profile.PostalCode = postal.Text.Trim();
                profile.City = city.Text.Trim();
                profile.Phone = phone.Text.Trim();
                profile.Email = email.Text.Trim();
                profile.LicenseNumber = license.Text.Trim();
                profile.Save();
                await UiAlerts.ShowSuccessAsync(XamlRoot, "Το προφίλ πρακτορείου αποθηκεύτηκε.");
            }
            catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
        };
        panel.Children.Add(save);
        Content = new ScrollViewer { Content = panel };
    }
}
