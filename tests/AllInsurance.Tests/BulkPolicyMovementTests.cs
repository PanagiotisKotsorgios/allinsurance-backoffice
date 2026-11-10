using AllInsurance.Application.OfficeOperations;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Tests;

public sealed class BulkPolicyMovementTests
{
    [Fact]
    public async Task PolicyBatchRejectsMissingPolicyWithoutPostingPartialPayments()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customerId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            db.Customers.Add(new Customer { Id = customerId, LastName = "Δοκιμή", FirstName = "Πελάτης" });
            db.BusinessRecords.Add(new BusinessRecord
            {
                Id = policyId, SectionKey = "policies", Title = "Ασφαλιστήριο δοκιμής",
                CustomerId = customerId
            });
            await db.SaveChangesAsync();
        }

        var service = new BulkFinancialMovementService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync("policies", "payment",
            [new BulkMovementItem(policyId, 40m), new BulkMovementItem(Guid.NewGuid(), 20m)],
            "Μετρητά", null, null, "EUR"));

        await using var verify = await environment.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await verify.CustomerTransactions.ToListAsync());
    }

    [Fact]
    public async Task PolicyBatchLinksEachReceiptToItsCustomerAndPolicy()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customerId = Guid.NewGuid();
        var policyIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            db.Customers.Add(new Customer { Id = customerId, LastName = "Δοκιμή", FirstName = "Πελάτης" });
            foreach (var id in policyIds)
                db.BusinessRecords.Add(new BusinessRecord
                {
                    Id = id, SectionKey = "policies", Title = $"Ασφαλιστήριο {id:N}",
                    CustomerId = customerId
                });
            await db.SaveChangesAsync();
        }

        var service = new BulkFinancialMovementService(environment.ContextFactory, environment.Session);
        var result = await service.PostAsync("policies", "receipt",
            [new BulkMovementItem(policyIds[0], 40m), new BulkMovementItem(policyIds[1], 60m)],
            "Μετρητά", null, null, "EUR");

        await using var verify = await environment.ContextFactory.CreateDbContextAsync();
        var movements = await verify.CustomerTransactions.AsNoTracking()
            .Where(item => result.MovementIds.Contains(item.Id)).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, item => Assert.Equal(customerId, item.CustomerId));
        Assert.Equal(policyIds.Order(), movements.Select(item => item.BusinessRecordId!.Value).Order());
        Assert.Equal(100m, movements.Sum(item => item.Amount));
        Assert.Equal(2, await verify.BusinessRecordActivities.CountAsync(item =>
            policyIds.Contains(item.RecordId) && item.Action == "Μαζική είσπραξη"));
    }
}
