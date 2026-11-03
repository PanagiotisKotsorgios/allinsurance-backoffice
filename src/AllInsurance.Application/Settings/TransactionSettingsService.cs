using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Settings;

public sealed class TransactionSettingsService(IApplicationDbContextFactory factory, ISessionService session)
    : ITransactionSettingsService
{
    public const string CurrencyKey = "transaction_currency";
    public static readonly string[] SupportedCurrencies = ["EUR", "USD", "GBP", "CHF"];

    public static string Symbol(string currency) => currency switch
    {
        "EUR" => "€", "USD" => "$", "GBP" => "£", "CHF" => "CHF", _ => currency
    };

    public async Task<string> GetCurrencyAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.AppMetadata.AsNoTracking().Where(item => item.Key == CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
    }

    public async Task SetCurrencyAsync(string currency, CancellationToken cancellationToken = default)
    {
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Μόνο ο διαχειριστής μπορεί να αλλάξει το νόμισμα.");
        currency = currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(currency))
            throw new ArgumentException("Επιλέξτε υποστηριζόμενο νόμισμα.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var current = await db.AppMetadata.SingleOrDefaultAsync(item => item.Key == CurrencyKey,
            cancellationToken);
        if ((current?.Value ?? "EUR") == currency) return;
        if (await db.CustomerTransactions.AnyAsync(cancellationToken) ||
            await db.Customers.AnyAsync(item => item.OpeningBalance != 0, cancellationToken))
            throw new InvalidOperationException(
                "Το νόμισμα δεν μπορεί να αλλάξει όσο υπάρχουν καταχωρισμένα υπόλοιπα ή κινήσεις. Δεν γίνεται αυτόματη μετατροπή ποσών.");
        if (current is null)
            db.AppMetadata.Add(new AppMetadata { Key = CurrencyKey, Value = currency });
        else current.Value = currency;
        await db.SaveChangesAsync(cancellationToken);
    }
}
