namespace AllInsurance.Tests;

public sealed class UserToolsFlowTests
{
    [Fact]
    public async Task AdministratorCanManageUserAndPreserveAuditOnDeletion()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var users = environment.UserAdministrationService;
        var id = await users.CreateUserAsync("employee.one", "Υπάλληλος Δοκιμή",
            "A temporary passphrase 2026!", ["Employee"]);
        await users.UpdateUserAsync(id, "employee.two", "Υπάλληλος Δεύτερος");
        await users.CreateRoleAsync("Supervisor", "Εσωτερική ομάδα");
        await users.AssignRoleAsync(id, "Supervisor");
        var account = Assert.Single(await users.GetUsersAsync(), item => item.Id == id);
        Assert.Equal("employee.two", account.Username);
        Assert.Contains("Supervisor", account.Roles);
        await users.DeactivateUserAsync(id);
        await users.DeleteUserAsync(id);
        Assert.DoesNotContain(await users.GetUsersAsync(), item => item.Id == id);
        var events = await users.GetSecurityEventsAsync();
        Assert.Contains(events, item => item.EventType == "USER_DELETED");
        Assert.Contains(events, item => item.EventType == "USER_CREATED" &&
            item.Username == "employee.two");
    }
}
