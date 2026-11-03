using AllInsurance.Application.Customers;
using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class FilterOptionFlowTests
{
    [Fact]
    public async Task LargeCityImportSurvivesReopen()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var filters = new FilterOptionService(environment.ContextFactory, environment.Session);
        var existing = (await filters.ListAsync("Πόλεις")).Count;
        var values = Enumerable.Range(0, 10_000).Select(index => $"Πόλη {index}").ToArray();
        Assert.Equal(10_000, await filters.ImportAsync("Πόλεις", values));
        Assert.Equal(existing + 10_000, (await filters.ListAsync("Πόλεις")).Count);
    }

    [Fact]
    public async Task OptionsArePersistedDeduplicatedAndCombinedWithCustomerValues()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var filters = new FilterOptionService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => filters.ListAsync("Πόλεις"));
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        Assert.Contains("Α.Ε.", await filters.ChoicesAsync("Νομικές μορφές"));
        Assert.Contains("Ελληνική", await filters.ChoicesAsync("Ιθαγένειες"));
        await customers.SaveAsync(new Customer { LastName = "Δοκιμή", FirstName = "Πόλης",
            TaxNumber = "100000005",
            City = "Μεσολόγγι", Occupation = "Λογιστής", PartnerName = "Συνεργάτης Α",
            BelongsToOffice = false, MaritalStatus = "Έγγαμος" });
        Assert.Contains("Μεσολόγγι", await filters.ChoicesAsync("Πόλεις"));
        Assert.Contains("Συνεργάτης Α", await filters.ChoicesAsync("Συνεργάτες"));
        var partners = new BusinessRecordService(environment.ContextFactory, environment.Session);
        await partners.SaveAsync(new BusinessRecord { SectionKey = "partners", Title = "Συνεργάτης Β" });
        Assert.Contains("Συνεργάτης Β", await filters.ChoicesAsync("Συνεργάτες"));
        await Assert.ThrowsAsync<ArgumentException>(() => filters.SaveAsync(null, "Συνεργάτες", "Χειροκίνητος"));
        Assert.Contains("Λογιστής", await filters.ChoicesAsync("Επαγγέλματα"));

        Assert.Equal(2, await filters.ImportAsync("Πόλεις", ["Δοκιμαστική Πόλη Άλφα", "δοκιμαστική πόλη άλφα", "Δοκιμαστική Πόλη Βήτα"]));
        Assert.Equal(0, await filters.ImportAsync("Πόλεις", ["Δοκιμαστική Πόλη Άλφα"]));
        var option = (await filters.ListAsync("Πόλεις")).Single(item => item.Value == "Δοκιμαστική Πόλη Άλφα");
        await filters.SaveAsync(option.Id, "Πόλεις", "Δοκιμαστική Πόλη Γάμμα");
        Assert.Contains("Δοκιμαστική Πόλη Γάμμα", await filters.ChoicesAsync("Πόλεις"));
        await filters.DeleteAsync(option.Id);
        Assert.DoesNotContain("Δοκιμαστική Πόλη Γάμμα", await filters.ChoicesAsync("Πόλεις"));
    }
}
