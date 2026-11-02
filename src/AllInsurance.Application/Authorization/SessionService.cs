namespace AllInsurance.Application.Authorization;

public sealed class SessionService : ISessionManager
{
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ADMINISTRATOR"] =
            [
                Permission.ManageUsers,
                Permission.AssignRoles,
                Permission.ResetCredentials,
                Permission.ReadSecurityEvents
            ],
            ["EMPLOYEE"] = []
        };

    public AuthenticatedSession? Current { get; private set; }
    public bool IsAuthenticated => Current is not null;
    public bool RequiresPasswordChange => PendingPasswordChangeUserId.HasValue && Current is null;
    public Guid? PendingPasswordChangeUserId { get; private set; }

    public void BeginPasswordChange(Guid userId)
    {
        Current = null;
        PendingPasswordChangeUserId = userId;
    }

    public void CompletePasswordChange(Guid userId, string username, string displayName, IEnumerable<string> roleNames)
    {
        if (PendingPasswordChangeUserId != userId)
        {
            throw new InvalidOperationException("There is no matching password-change session.");
        }

        PendingPasswordChangeUserId = null;
        SetAuthenticated(userId, username, displayName, roleNames);
    }

    public void EstablishSession(Guid userId, string username, string displayName, IEnumerable<string> roleNames)
    {
        PendingPasswordChangeUserId = null;
        SetAuthenticated(userId, username, displayName, roleNames);
    }

    public void RefreshRoles(Guid userId, IEnumerable<string> roleNames)
    {
        if (Current?.UserId != userId)
        {
            return;
        }

        SetAuthenticated(userId, Current.Username, Current.DisplayName, roleNames);
    }

    public void EndSession()
    {
        Current = null;
        PendingPasswordChangeUserId = null;
    }

    private void SetAuthenticated(Guid userId, string username, string displayName, IEnumerable<string> roleNames)
    {
        var roles = roleNames
            .Select(NormalizeRoleName)
            .ToHashSet(StringComparer.Ordinal);
        var permissions = roles
            .Where(RolePermissions.ContainsKey)
            .SelectMany(role => RolePermissions[role])
            .ToHashSet(StringComparer.Ordinal);
        Current = new AuthenticatedSession(userId, username, displayName, roles, permissions);
    }

    private static string NormalizeRoleName(string roleName) => roleName.Trim().ToUpperInvariant();
}
