using AllInsurance.Application.Receipts;
using AllInsurance.Domain.Entities;
using PdfSharp.Pdf.IO;

namespace AllInsurance.Tests;

public sealed class ReceiptPdfTests
{
    [Fact]
    public void GreekReceiptHasAgencyCustomerPaymentAndSignatureFields()
    {
        var transaction = new CustomerTransaction
        {
            Id = Guid.NewGuid(),
            Type = "receipt",
            Amount = 123.45m,
            PaymentMethod = "Ταχυπληρωμή",
            Currency = "EUR",
            Reference = "PAY-123",
            Description = "Είσπραξη ασφαλιστηρίου",
            OccurredAtUtc = DateTime.UtcNow
        };
        var issuer = new ReceiptIssuer("Δοκιμαστικό Πρακτορείο", "123456789",
            "Οδός 1", "12345", "Αθήνα", "2100000000", "info@example.test", "ABC123");
        var bytes = ReceiptPdfGenerator.Generate(transaction, "Δοκιμή Πελάτη", "POL-123", issuer);
        Assert.True(bytes.Length > 3000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.Single(pdf.Pages);
    }

    [Fact]
    public void PaymentCannotBeExportedAsReceipt()
    {
        var transaction = new CustomerTransaction { Type = "payment" };
        var issuer = new ReceiptIssuer("Agency", "123456789", "Address", "10000",
            "Athens", "", "", "");
        Assert.Throws<ArgumentException>(() => ReceiptPdfGenerator.Generate(
            transaction, "Customer", null, issuer));
    }
}
