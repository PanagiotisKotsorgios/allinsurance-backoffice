using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using AllInsurance.Infrastructure.Persistence;

namespace AllInsurance.Tests;

public sealed class BackupFlowTests
{
    [Fact]
    public async Task AdministratorCanCreateAndRestoreVerifiedBackup()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var backups = new BackupService(environment.DatabasePath, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => backups.CreateAsync());
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var backup = await backups.CreateAsync();
        Assert.True(File.Exists(backup.FullPath));
        Assert.True(backup.SizeBytes > 0);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Μετά", FirstName = "Αντίγραφο", TaxNumber = "100000061"
        });
        Assert.NotNull(await customers.GetAsync(customerId));
        await Assert.ThrowsAsync<ArgumentException>(() => backups.RestoreAsync("../other.db"));
        await backups.RestoreAsync(backup.FileName);
        Assert.Null(await customers.GetAsync(customerId));
        Assert.Equal(2, (await backups.ListAsync()).Count);
    }
}
