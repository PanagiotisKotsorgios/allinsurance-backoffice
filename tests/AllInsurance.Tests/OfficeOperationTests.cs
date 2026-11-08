using AllInsurance.Application.Business;
using AllInsurance.Application.OfficeOperations;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class OfficeOperationTests
{
    [Fact]
    public async Task OfficeIncomeAndExpenseStaySeparateFromInsuranceTransactions()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var service = new OfficeOperationService(environment.ContextFactory, environment.Session);
        var incomeId = await service.SaveAsync(new OfficeOperation
        { Kind = "income", OccurredOn = new DateTime(2026, 11, 5),
            Description = "Άλλο έσοδο", Category = "Λοιπά", Amount = 150 });
        var expenseId = await service.SaveAsync(new OfficeOperation
        { Kind = "expense", OccurredOn = new DateTime(2026, 11, 5),
            Description = "Αγορά εξοπλισμού", Category = "Εξοπλισμός", Amount = 40 });
        Assert.Equal(150m, (await service.ListAsync("income")).Single().Amount);
        Assert.Equal(40m, (await service.ListAsync("expense")).Single().Amount);
        Assert.Empty(await service.ListAsync("overcommission"));
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
            Assert.Empty(db.CustomerTransactions);
        var expense = (await service.GetAsync(expenseId))!;
        expense.Amount = 45;
        await service.SaveAsync(expense);
        Assert.Equal(45m, (await service.GetAsync(expenseId))!.Amount);
        await service.DeleteAsync(incomeId);
        Assert.Empty(await service.ListAsync("income"));
    }

    [Fact]
    public async Task OvercommissionRequiresRegisteredPartnerAndCompany()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var partnerId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "partners", Title = "Συνεργάτης δοκιμής" });
        var companyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Εταιρεία δοκιμής" });
        var service = new OfficeOperationService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new OfficeOperation
        { Kind = "overcommission", Description = "Λανθασμένη σύνδεση",
            PartnerId = Guid.NewGuid(), CompanyId = companyId, GrossPremium = 100,
            NetPremium = 80, PartnerCommission = 15, OverCommission = 5 }));
        var id = await service.SaveAsync(new OfficeOperation
        { Kind = "overcommission", Description = "",
            PartnerId = partnerId, CompanyId = companyId, GrossPremium = 100,
            NetPremium = 80, PartnerCommission = 15, OverCommission = 5 });
        var saved = (await service.GetAsync(id))!;
        Assert.Equal(5m, saved.Amount);
        Assert.Equal(15m, saved.PartnerCommission);
        Assert.Equal(partnerId, saved.PartnerId);
        Assert.Equal(companyId, saved.CompanyId);
    }
}
