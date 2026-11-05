using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class CustomerCodeTests
{
    [Fact]
    public async Task CustomCodePersistsAppearsInSearchAndMustBeUnique()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var service = new CustomerService(environment.ContextFactory, environment.Session);
        var id = await service.SaveAsync(new Customer
        {
            LastName = "Δοκιμή", FirstName = "Πρώτος", CustomCode = "CLIENT-42"
        });

        Assert.Equal("CLIENT-42", (await service.GetAsync(id))!.Code);
        Assert.Equal(id, Assert.Single(await service.SearchAsync("CLIENT-42")).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new Customer
        {
            LastName = "Δοκιμή", FirstName = "Δεύτερος", CustomCode = "client-42"
        }));
    }
}
