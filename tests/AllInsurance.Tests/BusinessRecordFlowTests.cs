using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class BusinessRecordFlowTests
{
    [Fact]
    public async Task SharedDashboardRecordsPersistAndLinkToCustomer()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => records.SearchAsync("vehicles"));
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Οδηγός", FirstName = "Δοκιμή"
        });
        var id = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "ΙΧ 1234", Reference = "ABC-1234",
            CustomerId = customerId, Status = "Ενεργό"
        });
        var item = Assert.Single(await records.SearchAsync("vehicles", "ABC-1234"));
        Assert.Equal(id, item.Id);
        Assert.Equal(customerId, item.CustomerId);
        Assert.Equal(customerId, (await customers.GetAsync(customerId))!.BusinessRecords.Single().CustomerId);
        Assert.Empty(await records.SearchAsync("policies"));
        item.Notes = "Νέα σημείωση";
        await records.SaveAsync(item);
        Assert.Equal("Νέα σημείωση", (await records.GetAsync(id))!.Notes);
        await records.DeleteAsync(id);
        Assert.Empty(await records.SearchAsync("vehicles"));
    }

    [Fact]
    public async Task InvalidSectionAndUnknownCustomerAreRejected()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "missing", Title = "Έλεγχος"
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "claims", Title = "Έλεγχος", CustomerId = Guid.NewGuid()
        }));
    }
}
