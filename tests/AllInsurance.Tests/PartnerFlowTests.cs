using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Partners;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class PartnerFlowTests
{
    [Fact]
    public async Task CustomerReferralTracksPartnerRenameAndPreventsDeletion()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var partnerId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "ΣΥΝΕΡΓΑΤΗΣ Α",
            DetailsJson = new PartnerProfileData { TaxNumber = "987654321" }.Write()
        });
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "ΔΟΚΙΜΗ", FirstName = "ΣΥΣΤΑΣΗ", TaxNumber = "100000089",
            Source = "Σύσταση από συνεργάτη", ReferredByPartnerName = "ΣΥΝΕΡΓΑΤΗΣ Α"
        });
        Assert.True((await customers.GetAsync(customerId))!.IsReferral);
        var partner = (await records.GetAsync(partnerId))!;
        partner.Title = "ΣΥΝΕΡΓΑΤΗΣ Β";
        await records.SaveAsync(partner);
        Assert.Equal("ΣΥΝΕΡΓΑΤΗΣ Β", (await customers.GetAsync(customerId))!.ReferredByPartnerName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(partnerId));
    }

    [Fact]
    public async Task PartnerProductionPayoutAndAuditRemainLinkedAfterRename()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var partners = new PartnerService(environment.ContextFactory, environment.Session);
        var filters = new FilterOptionService(environment.ContextFactory, environment.Session);
        Assert.NotEmpty(await filters.ChoicesAsync("Κατηγορίες συνεργατών"));
        var partnerId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "ΔΟΚΙΜΗ ΣΥΝΕΡΓΑΤΗΣ", Status = "Ενεργός",
            DetailsJson = new PartnerProfileData
            {
                FirstName = "Συνεργάτης", LastName = "Δοκιμή", TaxNumber = "123456789",
                DefaultCommissionPercent = 12m, GemiNumber = "123456789012",
                BirthPlace = "Πάτρα", Nationality = "Ελληνική",
                BankAccounts =
                [
                    new PartnerBankAccount { BankName = "Πρώτη", Iban = "GR1601101250000000012300695" },
                    new PartnerBankAccount { BankName = "Δεύτερη", Iban = "GR9608100010000001234567890" }
                ]
            }.Write()
        });
        var persistedProfile = PartnerProfileData.Read((await records.GetAsync(partnerId))!.DetailsJson);
        Assert.Equal(2, persistedProfile.BankAccounts.Count);
        Assert.Equal("123456789012", persistedProfile.GemiNumber);
        Assert.Equal("Πάτρα", persistedProfile.BirthPlace);
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "ΔΕΥΤΕΡΟΣ",
            DetailsJson = new PartnerProfileData { TaxNumber = "123456789" }.Write()
        }));
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Πελάτης", FirstName = "Δοκιμή", TaxNumber = "100000041",
            PartnerName = "ΔΟΚΙΜΗ ΣΥΝΕΡΓΑΤΗΣ", BelongsToOffice = false
        });
        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "POL-PARTNER-1", CustomerId = customerId,
            DocumentType = "Ασφαλιστήριο", Status = "Ενεργό",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                PolicyNumber = "POL-PARTNER-1", GrossPremium = 1000m
            })
        });
        Assert.Equal("ΔΟΚΙΜΗ ΣΥΝΕΡΓΑΤΗΣ", (await customers.GetAsync(customerId))!.PartnerName);
        var policyDetails = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(partnerId, policyDetails.PartnerId);
        Assert.Equal(12m, policyDetails.PartnerCommissionPercent);
        var snapshot = await partners.GetSnapshotAsync(partnerId);
        Assert.Equal(120m, snapshot.Earned);
        Assert.Equal(120m, snapshot.Due);
        Assert.Equal(partnerId, BusinessRecordDetails.Read<PolicyCardData>(
            (await records.GetAsync(policyId))!.DetailsJson).PartnerId);
        await partners.AddPayoutAsync(partnerId, 50m, "Τραπεζική μεταφορά", "TX-1", "Μέρος προμήθειας");
        snapshot = await partners.GetSnapshotAsync(partnerId);
        Assert.Equal(50m, snapshot.Paid);
        Assert.Equal(70m, snapshot.Due);
        Assert.Contains(snapshot.Activities, item => item.Action == "Γενική πληρωμή");
        await partners.AddMovementAsync(partnerId, "receipt", 20m, "Μετρητά", "RC-1", "Γενική είσπραξη");
        snapshot = await partners.GetSnapshotAsync(partnerId);
        Assert.Equal(30m, snapshot.Paid);
        Assert.Equal(90m, snapshot.Due);
        Assert.Contains(snapshot.Payouts, item => item.Kind == "receipt");
        Assert.Contains(snapshot.Activities, item => item.Action == "Γενική είσπραξη");
        var partner = (await records.GetAsync(partnerId))!;
        partner.Title = "ΝΕΟ ΟΝΟΜΑ";
        partner.Status = "Αναστολή";
        await records.SaveAsync(partner);
        Assert.Equal("ΝΕΟ ΟΝΟΜΑ", (await customers.GetAsync(customerId))!.PartnerName);
        Assert.Equal(120m, (await partners.GetSnapshotAsync(partnerId)).Earned);
        Assert.Contains((await partners.GetSnapshotAsync(partnerId)).Activities,
            item => item.Description.Contains("Κατάσταση"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(partnerId));
    }
}
