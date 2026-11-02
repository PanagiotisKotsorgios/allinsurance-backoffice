namespace AllInsurance.Application.Authorization;

public static class Permission
{
    public const string ManageUsers = "users.manage";
    public const string AssignRoles = "roles.assign";
    public const string ResetCredentials = "users.reset-credentials";
    public const string ReadSecurityEvents = "security-events.read";
}
