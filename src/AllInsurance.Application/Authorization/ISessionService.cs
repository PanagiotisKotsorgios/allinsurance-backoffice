namespace AllInsurance.Application.Authorization;

public sealed record AuthenticatedSession(
    Guid UserId,
    string Username,
    string DisplayName,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions);

public interface ISessionService
{
    AuthenticatedSession? Current { get; }
    bool IsAuthenticated { get; }
    bool RequiresPasswordChange { get; }
    void EndSession();
}

public interface ISessionManager : ISessionService
{
    Guid? PendingPasswordChangeUserId { get; }
    void BeginPasswordChange(Guid userId);
    void CompletePasswordChange(Guid userId, string username, string displayName, IEnumerable<string> roleNames);
    void EstablishSession(Guid userId, string username, string displayName, IEnumerable<string> roleNames);
    void RefreshRoles(Guid userId, IEnumerable<string> roleNames);
}
