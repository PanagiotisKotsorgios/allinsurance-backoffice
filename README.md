# allinsurance-backoffice

AllInsurance Backoffice Software is a Windows desktop application with Phase 01 authentication and a Phase 02 Greek workspace UI. It is native WinUI 3, uses a local SQLite database, and keeps application services independently testable.

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

To create or refresh a desktop shortcut with the supplied logo:

```powershell
.\scripts\create-desktop-shortcut.ps1
```

On the first launch, create the administrator and save the one-time recovery code before continuing to sign in. If setup is interrupted before account creation, it resumes on the next launch. No default administrator or password is provisioned. The database is stored per Windows user at `%LOCALAPPDATA%\AllInsurance\Data\allinsurance.db`.

The login flow supports local authentication, five-attempt/15-minute lockout, temporary-password replacement, and offline recovery-code rotation. A successful sign-in maximizes the Greek workspace with ten business sections and the supplied colorful section icons. The top bar provides navigation, administrator user management, audit log, system settings, keyboard shortcuts, and KALYPSIS SOFTWARE SOLUTIONS help and contact details. A footer displays database connection status and the active user. Closing the window ends the in-memory session.

The authentication lockout values can be changed in `src\AllInsurance.Desktop\appsettings.json`. Application services enforce authorization on user and role management. The business section screens have search layouts and empty states; record persistence is planned after the corresponding data rules are defined. See [Phase 02 analysis](Software%20Analysis/02%20-%20Workspace%20UI/README.md).

## Build and tests

```powershell
dotnet build AllInsurance.slnx
dotnet test tests\AllInsurance.Tests\AllInsurance.Tests.csproj
```

The integration tests use isolated temporary SQLite files and cover first-run setup, persistence, authentication, lockout, recovery, forced password changes, authorization, and last-administrator protection.

## Local security boundary

The SQLite file is not encrypted. It is stored under the current Windows user's Local AppData directory; use Windows account protections and BitLocker where available. Application permissions do not prevent someone with direct filesystem write access from tampering with the database. Do not place this SQLite file on a writable network share or store customer/insurance records in this Phase 01 prototype.
