using AllInsurance.Application.Authentication;

namespace AllInsurance.Application.Users;

public interface IUserAdministrationService
{
    Task<Guid> CreateUserAsync(
        string username,
        string displayName,
        string temporaryPassword,
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
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
