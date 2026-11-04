using AllInsurance.Application.Customers;
using AllInsurance.Application.Settings;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class CustomerFlowTests
{
    [Fact]
    public async Task CustomerRequiresTaxNumber()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var service = new CustomerService(environment.ContextFactory, environment.Session);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new Customer
        {
            LastName = "Δοκιμή", FirstName = "ΑΦΜ"
        }));
        Assert.Contains("ΑΦΜ", error.Message);
    }

    [Fact]
    public async Task CustomerRequiresSignInAndPersistsAcrossContexts()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var service = new CustomerService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchAsync());
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");

        var id = await service.SaveAsync(new Customer
        {
            LastName = "Παπαδόπουλος", FirstName = "Νίκος",
            TaxNumber = "123456789", PrimaryMobile = "6900000000",
            ThirdMobile = "6970000000",
            OpeningBalance = 100m, City = "Πάτρα", CustomerKind = "Φυσικό πρόσωπο",
            HomePhone = "2610000000", MaritalStatus = "Έγγαμος", Source = "Σύσταση",
            PassportNumber = "AA123456", Zone = "Αιτωλοακαρνανία", HasDrivingLicense = true
        });
        var loaded = await service.GetAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal("Παπαδόπουλος", loaded.LastName);
        Assert.Equal("Πάτρα", loaded.City);
        Assert.Equal("Έγγαμος", loaded.MaritalStatus);
        Assert.Equal("AA123456", loaded.PassportNumber);
        Assert.Equal("6970000000", loaded.ThirdMobile);
        Assert.True(loaded.HasDrivingLicense);
        Assert.Contains("2610000000", (await service.SearchAsync()).Single().SearchText);
        Assert.Contains("6970000000", (await service.SearchAsync()).Single().SearchText);
        Assert.Equal(100m, CustomerService.Balance(loaded));
        Assert.Single(await service.SearchAsync("123456789"));
        Assert.Single(await service.SearchAsync("Πάτρα"));
        var code = (await service.SearchAsync("Παπαδόπουλος")).Single().Code;
        Assert.Single(await service.SearchAsync(code));
    }

    [Fact]
    public async Task ReferralRelatedItemsAndLedgerSurviveReopen()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var service = new CustomerService(environment.ContextFactory, environment.Session);
        var referrerId = await service.SaveAsync(new Customer { LastName = "Πρώτος", FirstName = "Πελάτης",
            TaxNumber = "100000001" });
        var customerId = await service.SaveAsync(new Customer
        {
            LastName = "Δεύτερος", FirstName = "Πελάτης",
            TaxNumber = "100000002",
            IsReferral = true, ReferredByCustomerId = referrerId,
            OpeningBalance = 50m
        });
        var detail = await service.AddDetailAsync(customerId, "vehicles", "ΙΧ 1234", "Όχημα δοκιμής",
            null, null, null, false);
        await service.UpdateDetailAsync(customerId, detail.Id, "ΙΧ 1234", "Νέα περιγραφή",
            null, null, null, true);
        await service.AddTransactionAsync(customerId, "receipt", 20m, "Είσπραξη",
            "Τραπεζική μεταφορά", "BANK-123");
        await service.AddTransactionAsync(customerId, "payment", 5m, "Πληρωμή");

        var loaded = await service.GetAsync(customerId);
        Assert.NotNull(loaded);
        Assert.Equal(referrerId, loaded.ReferredByCustomerId);
        Assert.Single(loaded.Details);
        Assert.True(loaded.Details.Single().IsHistorical);
        Assert.Equal(35m, CustomerService.Balance(loaded));
        Assert.Equal(2, loaded.Transactions.Count);
        var receipt = Assert.Single(loaded.Transactions, item => item.Type == "receipt");
        Assert.Equal("Τραπεζική μεταφορά", receipt.PaymentMethod);
        Assert.Equal("BANK-123", receipt.Reference);
        Assert.Equal("EUR", receipt.Currency);
        Assert.Equal(2, (await service.GetStatisticsAsync()).Total);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(referrerId));
        await service.DeleteDetailAsync(customerId, detail.Id);
        await service.DeleteAsync(customerId);
        Assert.Null(await service.GetAsync(customerId));
        Assert.Empty((await service.GetAsync(referrerId))!.Details);
    }

    [Fact]
    public async Task CurrencyCanBeChangedOnlyBeforeMoneyIsRecorded()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var settings = new TransactionSettingsService(environment.ContextFactory, environment.Session);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);

        Assert.Equal("EUR", await settings.GetCurrencyAsync());
        await settings.SetCurrencyAsync("USD");
        Assert.Equal("USD", await settings.GetCurrencyAsync());

        var id = await customers.SaveAsync(new Customer
        {
            LastName = "Currency", FirstName = "Test", TaxNumber = "100000005"
        });
        await customers.AddTransactionAsync(id, "receipt", 10m, "Payment", "Cash", "TX-1");
        var customer = await customers.GetAsync(id);
        Assert.Equal("USD", Assert.Single(customer!.Transactions).Currency);
        await Assert.ThrowsAsync<InvalidOperationException>(() => settings.SetCurrencyAsync("EUR"));
    }

    [Fact]
    public async Task DuplicateTaxNumberAndStaleUpdatesAreRejected()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var service = new CustomerService(environment.ContextFactory, environment.Session);
        var id = await service.SaveAsync(new Customer
        {
            LastName = "Α", FirstName = "Β", TaxNumber = "123456789"
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new Customer
        {
            LastName = "Γ", FirstName = "Δ", TaxNumber = "123456789"
        }));
        var first = (await service.GetAsync(id))!;
        var stale = (await service.GetAsync(id))!;
        first.PrimaryMobile = "6900000000";
        await service.SaveAsync(first);
        Assert.Contains(await service.GetActivitiesAsync(id), item =>
            item.Action == "Επεξεργασία πελάτη" &&
            item.Description.Contains("κινητό") && item.Description.Contains("6900000000"));
        stale.PrimaryMobile = "6900000001";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(stale));
    }

    [Fact]
    public async Task PartnerOwnershipRequiresPartnerName()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<ArgumentException>(() => customers.SaveAsync(new Customer
        {
            LastName = "Δοκιμή", FirstName = "Συνεργάτη", BelongsToOffice = false,
            TaxNumber = "100000003"
        }));
    }

    [Fact]
    public async Task AttachedFileIsCopiedAndRemovedWithCustomer()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var folder = Path.GetDirectoryName(environment.DatabasePath)!;
        var fileRoot = Path.Combine(folder, "customer-files");
        var sourcePath = Path.Combine(folder, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "test attachment");
        var service = new CustomerService(environment.ContextFactory, environment.Session, fileRoot);
        var customerId = await service.SaveAsync(new Customer { LastName = "Αρχείο", FirstName = "Δοκιμή",
            TaxNumber = "100000004" });
        var attachment = await service.AddFileAsync(customerId, sourcePath);
        Assert.Equal("test attachment", await File.ReadAllTextAsync(service.GetFilePath(attachment)));
        Assert.Single((await service.GetAsync(customerId))!.Details);
        await service.DeleteAsync(customerId);
        Assert.False(File.Exists(service.GetFilePath(attachment)));
    }
}
