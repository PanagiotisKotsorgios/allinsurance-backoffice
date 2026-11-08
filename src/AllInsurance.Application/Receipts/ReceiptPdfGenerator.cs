using AllInsurance.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AllInsurance.Application.Receipts;

public sealed record ReceiptIssuer(string Name, string TaxNumber, string Address,
    string PostalCode, string City, string Phone, string Email, string LicenseNumber);

public static class ReceiptPdfGenerator
{
    static ReceiptPdfGenerator() => GlobalFontSettings.UseWindowsFontsUnderWindows = true;

    public static byte[] Generate(CustomerTransaction transaction, string customerName,
        string? policyNumber, ReceiptIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(issuer);
        if (transaction.Type != "receipt")
            throw new ArgumentException("Απόδειξη εξάγεται μόνο για είσπραξη.", nameof(transaction));
        if (string.IsNullOrWhiteSpace(issuer.Name) || string.IsNullOrWhiteSpace(issuer.TaxNumber) ||
            string.IsNullOrWhiteSpace(issuer.Address) || string.IsNullOrWhiteSpace(issuer.City))
            throw new ArgumentException("Συμπληρώστε πρώτα τα στοιχεία του πρακτορείου.", nameof(issuer));

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        var regular = new XFont("Arial", 11, XFontStyleEx.Regular);
        var bold = new XFont("Arial", 11, XFontStyleEx.Bold);
        var title = new XFont("Arial", 19, XFontStyleEx.Bold);
        var navy = new XSolidBrush(XColor.FromArgb(27, 65, 98));
        const double left = 48;
        var width = page.Width.Point - 96;

        void Line(string label, string? value, double y)
        {
            graphics.DrawString(label, bold, XBrushes.Black,
                new XRect(left, y, 180, 24), XStringFormats.TopLeft);
            new XTextFormatter(graphics).DrawString(
                string.IsNullOrWhiteSpace(value) ? "—" : value, regular,
                XBrushes.Black, new XRect(left + 184, y, width - 184, 34));
        }

        graphics.DrawRectangle(navy, 0, 0, page.Width.Point, 95);
        graphics.DrawString(issuer.Name, title, XBrushes.White,
            new XRect(left, 20, width, 31), XStringFormats.TopLeft);
        graphics.DrawString($"{issuer.Address}, {issuer.PostalCode} {issuer.City}  ·  ΑΦΜ {issuer.TaxNumber}",
            regular, XBrushes.White, new XRect(left, 58, width, 22), XStringFormats.TopLeft);
        graphics.DrawString("ΑΠΟΔΕΙΞΗ ΕΙΣΠΡΑΞΗΣ", title, navy,
            new XRect(left, 125, width, 32), XStringFormats.TopLeft);
        graphics.DrawString($"Αριθμός: {transaction.Id.ToString("N")[..12].ToUpperInvariant()}",
            regular, XBrushes.Black, new XRect(left, 164, width, 24), XStringFormats.TopLeft);
        graphics.DrawLine(XPens.LightGray, left, 196, left + width, 196);
        Line("Ημερομηνία", transaction.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 215);
        Line("Πελάτης", customerName, 252);
        Line("Ασφαλιστήριο", policyNumber, 289);
        Line("Ποσό", $"{transaction.Amount:N2} {transaction.Currency}", 326);
        Line("Τρόπος πληρωμής", transaction.PaymentMethod, 363);
        Line("Αριθμός αναφοράς", transaction.Reference, 400);
        Line("Αιτιολογία", transaction.Description, 437);
        graphics.DrawLine(XPens.LightGray, left, 505, left + width, 505);
        graphics.DrawString("Στοιχεία πρακτορείου", bold, navy,
            new XRect(left, 526, width, 23), XStringFormats.TopLeft);
        new XTextFormatter(graphics).DrawString(
            $"Τηλ.: {issuer.Phone}   Email: {issuer.Email}   Άδεια: {issuer.LicenseNumber}",
            regular, XBrushes.Black, new XRect(left, 555, width, 55));
        const double signatureY = 691;
        graphics.DrawLine(XPens.DarkGray, left, signatureY, left + 195, signatureY);
        graphics.DrawLine(XPens.DarkGray, left + width - 195, signatureY, left + width, signatureY);
        graphics.DrawString("Υπογραφή πελάτη", regular, XBrushes.Black,
            new XRect(left, signatureY + 9, 195, 25), XStringFormats.Center);
        graphics.DrawString("Υπογραφή πρακτορείου", regular, XBrushes.Black,
            new XRect(left + width - 195, signatureY + 9, 195, 25), XStringFormats.Center);
        var attribution = new XFont("Arial", 8, XFontStyleEx.Regular);
        graphics.DrawString("© Acalypso Software Solutions · www.mykalypsis.gr · info@mykalypsis.gr",
            attribution, XBrushes.Gray,
            new XRect(left, page.Height.Point - 42, width, 18), XStringFormats.TopRight);
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
