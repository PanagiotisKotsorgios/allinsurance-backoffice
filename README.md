# allinsurance-backoffice

AllInsurance Backoffice Software is a Windows desktop application with Phase 01 authentication, a Phase 02 Greek workspace UI, and Phase 03 customer records and shared dashboard functions. It is native WinUI 3, uses a local SQLite database, and keeps application services independently testable.

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

The authentication lockout values can be changed in `src\AllInsurance.Desktop\appsettings.json`. Application services enforce authorization on user and role management. The customer screen supports persisted profiles, linked records, financial movements, file attachments, and statistics. Other dashboards have a common persisted record workflow with customer links, ready for specialized fields in later phases. See [Phase 02 analysis](Software%20Analysis/02%20-%20Workspace%20UI/README.md) and [Phase 03 analysis](Software%20Analysis/03%20-%20Customers%20and%20Shared%20Dashboards/README.md).

Receipts and payments now record a payment method, currency, reference number, and notes. The balance refreshes immediately in the open customer card and customer list. EUR is the default currency; an administrator can change it in system settings before any monetary records exist. See [customer payments](Software%20Analysis/06%20-%20Customer%20Payments/README.md).

## Build and tests

```powershell
dotnet build AllInsurance.slnx
dotnet test tests\AllInsurance.Tests\AllInsurance.Tests.csproj
```

The integration tests use isolated temporary SQLite files and cover first-run setup, persistence, authentication, lockout, recovery, forced password changes, authorization, and last-administrator protection.

## Local security boundary

The SQLite file and customer attachments are not encrypted by the app. They are stored under the current Windows user's Local AppData directory; use Windows account protections and BitLocker where available. Application permissions do not prevent someone with direct filesystem write access from tampering with local records. Do not place the database on a writable network share.
