using AllInsurance.Application.Authentication;

namespace AllInsurance.Application.Users;

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserAccountSummary>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateUserAsync(
        string username,
        string displayName,
        string temporaryPassword,
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(Guid userId, string username, string displayName,
        CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task CreateRoleAsync(string name, string description, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task RevokeRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task ResetEmployeePasswordAsync(
        Guid userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityEventSummary>> GetSecurityEventsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record UserAccountSummary(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    DateTime? LastLoginAtUtc = null,
    DateTime? LockoutUntilUtc = null,
    int FailedLoginCount = 0,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null);
