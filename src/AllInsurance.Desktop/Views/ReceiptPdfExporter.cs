using AllInsurance.Application.Exports;
using AllInsurance.Application.Receipts;
using AllInsurance.Desktop.Settings;
using AllInsurance.Domain.Entities;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AllInsurance.Desktop.Views;

internal static class ReceiptPdfExporter
{
    public static async Task<bool> ExportAsync(Microsoft.UI.Xaml.Window owner,
        CustomerTransaction transaction, string customerName, string? policyNumber)
    {
        var profile = AgencyProfileSettings.Load();
        profile.EnsureReceiptReady();
        var picker = new FileSavePicker { SuggestedFileName =
            ExportFileName.Create("Είσπραξη", transaction.OccurredAtUtc.ToLocalTime()) +
            "-" + transaction.Id.ToString("N")[..8] };
        picker.FileTypeChoices.Add("Αρχείο PDF", [".pdf"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
        var target = await picker.PickSaveFileAsync();
        if (target is null) return false;
        var issuer = new ReceiptIssuer(profile.Name, profile.TaxNumber, profile.Address,
            profile.PostalCode, profile.City, profile.Phone, profile.Email, profile.LicenseNumber);
        await File.WriteAllBytesAsync(target.Path,
            ReceiptPdfGenerator.Generate(transaction, customerName, policyNumber, issuer));
        return true;
    }
}
