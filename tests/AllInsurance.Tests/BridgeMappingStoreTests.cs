using AllInsurance.Application.Bridges;
using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class BridgeMappingStoreTests
{
    [Fact]
    public async Task MappingsCanBeCreatedEditedAndRemovedWithoutChangingOtherOptions()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var filters = new FilterOptionService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var store = new BridgeMappingStore(filters, records);

        await store.SaveAsync(null, NativeBridgeSource.Ydrogeios,
            BridgeMappingKind.Branch, "30400", "Αυτοκίνητο", null, true);
        var branch = Assert.Single(await store.ListAsync());
        Assert.True(branch.IsValid);
        Assert.Equal("Αυτοκίνητο", branch.Target);
        Assert.Equal("30400|Αυτοκίνητο", Assert.Single(await filters.ListAsync(
            BridgeMappingStore.Category(NativeBridgeSource.Ydrogeios,
                BridgeMappingKind.Branch))).Value);
        Assert.DoesNotContain(await filters.CategoriesAsync(),
            value => value.StartsWith("bridge:", StringComparison.Ordinal));

        await store.SaveAsync(branch.Id, NativeBridgeSource.Ydrogeios,
            BridgeMappingKind.Branch, "30400", "Πυρός", null, true);
        branch = Assert.Single(await store.ListAsync());
        Assert.Equal("Πυρός", branch.Target);
        Assert.Contains("Αυτοκίνητο", await filters.ScopedChoicesAsync("Κλάδοι", "Υδρόγειος", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(null,
            NativeBridgeSource.Ydrogeios, BridgeMappingKind.Branch,
            "30400", "Πυρός", null, false));

        var partnerId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "Δοκιμαστικός συνεργάτης",
            DetailsJson = new PartnerProfileData { TaxNumber = "123456789" }.Write()
        });
        await store.SaveAsync(null, NativeBridgeSource.Orizon,
            BridgeMappingKind.Partner, "022", null, partnerId, false);
        var partner = Assert.Single(await store.ListAsync(), item =>
            item.Kind == BridgeMappingKind.Partner);
        Assert.Equal(partnerId, partner.PartnerId);
        Assert.True(partner.IsValid);

        await store.DeleteAsync(branch.Id);
        Assert.Single(await store.ListAsync());
        Assert.Contains("Πυρός", await filters.ScopedChoicesAsync("Κλάδοι", "Υδρόγειος", null));
    }
}
