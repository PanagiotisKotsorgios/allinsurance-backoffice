namespace AllInsurance.Desktop.Security;

public enum RecoveryCodeFlow
{
    AdministratorSetup,
    PasswordRecovery,
    ForcedPasswordChange,
    OwnCodeRotation
}

public sealed record RecoveryCodeDisplay(string Code, RecoveryCodeFlow Flow);

public sealed class RecoveryCodeDisplayState
{
    public RecoveryCodeDisplay? Current { get; private set; }

    public void Show(string code, RecoveryCodeFlow flow)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("A one-time recovery code is already awaiting acknowledgment.");
        }

        Current = new RecoveryCodeDisplay(code, flow);
    }

    public void Clear() => Current = null;
}
