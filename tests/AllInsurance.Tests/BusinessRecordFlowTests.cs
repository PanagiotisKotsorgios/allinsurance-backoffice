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
            LastName = "Οδηγός", FirstName = "Δοκιμή", TaxNumber = "100000006"
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

    [Fact]
    public async Task ApplicationConversionAndLinkedPaymentAreAudited()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Policy", FirstName = "Owner", TaxNumber = "100000021"
        });
        var otherId = await customers.SaveAsync(new Customer
        {
            LastName = "Other", FirstName = "Owner", TaxNumber = "100000022"
        });
        var otherVehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "OTHER-1", CustomerId = otherId
        });
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "APP-INVALID", CustomerId = customerId,
            DocumentType = "Αίτηση",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                ApplicationNumber = "APP-INVALID", VehicleId = otherVehicleId
            })
        }));
        var vehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "ABC-1234", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
            {
                Registration = "ABC-1234", Make = "Test"
            })
        });
        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "APP-1", Reference = "APP-1",
            DocumentType = "Αίτηση", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                ApplicationNumber = "APP-1", VehicleId = vehicleId
            })
        });
        await Assert.ThrowsAsync<ArgumentException>(() => customers.AddTransactionAsync(
            otherId, "payment", 15m, "Wrong owner", "Μετρητά", null, policyId));
        await Assert.ThrowsAsync<ArgumentException>(() => customers.AddTransactionAsync(
            customerId, "payment", 15m, "Application only", "Μετρητά", null, policyId));
        var application = (await records.GetAsync(policyId))!;
        application.DocumentType = "Ασφαλιστήριο";
        application.Title = application.Reference = "POL-1";
        application.DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
        {
            ApplicationNumber = "APP-1", PolicyNumber = "POL-1", VehicleId = vehicleId
        });
        await records.SaveAsync(application);
        await customers.AddTransactionAsync(customerId, "payment", 15m, "Premium",
            "Τραπεζική μεταφορά", "TX-42", policyId);

        var loaded = await customers.GetAsync(customerId);
        Assert.Equal(policyId, Assert.Single(loaded!.Transactions).BusinessRecordId);
        Assert.Equal("Ασφαλιστήριο", (await records.GetAsync(policyId))!.DocumentType);
        var history = await customers.GetActivitiesAsync(customerId);
        Assert.Contains(history, item => item.Description.Contains("μετατράπηκε"));
        Assert.Contains(history, item => item.Action == "Πληρωμή" && item.Description.Contains("POL-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(policyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(vehicleId));
    }
}
