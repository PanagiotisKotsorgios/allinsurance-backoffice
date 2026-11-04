using AllInsurance.Domain.Entities;
using AllInsurance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Tests;

public sealed class AuditFlowTests
{
    [Fact]
    public async Task DataChangesRecordActorEntityAndChangedFields()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var options = new DbContextOptionsBuilder<AllInsuranceDbContext>()
            .UseSqlite($"Data Source={environment.DatabasePath};Foreign Keys=True").Options;
        var customerId = Guid.NewGuid();
        await using (var db = new AllInsuranceDbContext(options, environment.Session))
        {
            db.Customers.Add(new Customer { Id = customerId, LastName = "Δοκιμή",
                FirstName = "Ελέγχου", TaxNumber = "100000099" });
            await db.SaveChangesAsync();
        }
        await using (var db = new AllInsuranceDbContext(options, environment.Session))
        {
            var customer = await db.Customers.SingleAsync(item => item.Id == customerId);
            customer.FirstName = "Ενημέρωση";
            await db.SaveChangesAsync();
            var audit = await db.SecurityEvents.Where(item => item.UserId == environment.Session.Current!.UserId &&
                item.EventType.EndsWith(":Customer")).OrderBy(item => item.CreatedAtUtc).ToListAsync();
            Assert.Contains(audit, item => item.EventType == "CREATE:Customer");
            Assert.Contains(audit, item => item.EventType == "UPDATE:Customer" &&
                item.Details!.Contains("FirstName"));
        }
    }
}
