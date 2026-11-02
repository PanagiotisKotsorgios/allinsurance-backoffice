# allinsurance-backoffice

All Insurance Back Office is a Windows desktop prototype for Phase 01 (core foundation and authentication). The current implementation is native WinUI 3, uses a local SQLite database, and keeps application services independently testable.

## Requirements

- Windows 10 version 19041 or newer
- .NET 10 SDK
- Windows App SDK 2.6.4 (restored from NuGet)

## Run locally

From the repository root:

```powershell
dotnet restore AllInsurance.slnx
dotnet run --project src\AllInsurance.Desktop\AllInsurance.Desktop.csproj
```

On the first launch, create the administrator and save the one-time recovery code before continuing to sign in. If setup is interrupted before account creation, it resumes on the next launch. No default administrator or password is provisioned. The database is stored per Windows user at `%LOCALAPPDATA%\AllInsurance\Data\allinsurance.db`.

The login flow supports local authentication, five-attempt/15-minute lockout, temporary-password replacement, and offline recovery-code rotation. A successful sign-in opens only a plain white workspace. Closing the window ends the in-memory session.

The authentication lockout values can be changed in `src\AllInsurance.Desktop\appsettings.json`. Application services enforce authorization; user/role administration has no management UI in this phase.

## Build and tests

```powershell
dotnet build AllInsurance.slnx
dotnet test tests\AllInsurance.Tests\AllInsurance.Tests.csproj
```

The integration tests use isolated temporary SQLite files and cover first-run setup, persistence, authentication, lockout, recovery, forced password changes, authorization, and last-administrator protection.

## Local security boundary

The SQLite file is not encrypted. It is stored under the current Windows user's Local AppData directory; use Windows account protections and BitLocker where available. Application permissions do not prevent someone with direct filesystem write access from tampering with the database. Do not place this SQLite file on a writable network share or store customer/insurance records in this Phase 01 prototype.
