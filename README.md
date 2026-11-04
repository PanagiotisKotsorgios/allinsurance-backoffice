# allinsurance-backoffice

AllInsurance Backoffice Software is a Windows desktop application with Greek authentication, customer and partner workflows, and connected cards for vehicles, insurance documents, companies, agencies, claims, updates, reports, and company bridges. It is native WinUI 3, uses a local SQLite database, and keeps application services independently testable.

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

The authentication lockout values can be changed in `src\AllInsurance.Desktop\appsettings.json`. Application services enforce authorization on user and role management. The customer screen supports persisted profiles, linked records, financial movements, file attachments, and statistics. The remaining workspace cards include section-specific fields, linked records, search, pagination, and activity history. See [workspace UI](Software%20Analysis/02%20-%20Workspace%20UI/README.md), [customer workflows](Software%20Analysis/03%20-%20Customers%20and%20Shared%20Dashboards/README.md), and [connected workspace cards](Software%20Analysis/13%20-%20Connected%20Workspace%20Cards/README.md).

Receipts and payments now record a payment method, currency, reference number, and notes. The balance refreshes immediately in the open customer card and customer list. EUR is the default currency; an administrator can change it in system settings before any monetary records exist. See [customer payments](Software%20Analysis/06%20-%20Customer%20Payments/README.md).

Customer and section tables support dragging column edges and row bottoms to resize them; the chosen sizes are stored per Windows user. The workspace window now maximizes before navigation, so sidebar labels appear with the first workspace frame and a brief entrance animation.

The customer card now opens dedicated policy and vehicle cards. New policy records start as applications, and an explicit action converts them to insurance policies. Receipts and payments can be linked to a policy belonging to the same customer. Customer activities are recorded with timestamp, action, details, and user; earlier edits cannot be reconstructed from databases created before this audit feature. See [customer card workflow](Software%20Analysis/08%20-%20Customer%20Card%20Workflow/README.md).

All shared dashboards now use the customer screen's action layout and per-section column visibility. The partner card tracks identity, cooperation, production, policies, earned commissions, payouts and activity history. Partner categories and professional roles are configurable. See [partner workspace](Software%20Analysis/09%20-%20Partner%20Workspace/README.md).

The customer, partner, vehicle and policy cards share the same form layout, tab style and red required-field markers. The navigation menu mirrors every sidebar section and its shortcut. Administrator tools now include SQLite backups with integrity checks and a searchable local error log. The customer table no longer displays the mobile column. See [navigation and maintenance](Software%20Analysis/10%20-%20Navigation%20and%20Maintenance/README.md).

The partner card now places cooperation details and multiple bank accounts in the main card, with extra identity and contact fields in an expandable section. Production is shown with financial movements; general receipts and payments update the partner balance. The vehicle card stores technical and equipment details, an optional local KTEO image, multiple owners and drivers with ownership shares, linked policy cards, and linked claims with add/edit/delete actions. See [partner and vehicle card expansion](Software%20Analysis/11%20-%20Partner%20and%20Vehicle%20Cards/README.md).

The login action is larger and centered. The partner's financial tab now shows a dated ledger of earned commissions, payouts, receipts and running balance. A vehicle can open a new insurance application with its primary owner and vehicle already selected. Administrators can maintain commission rules by insurer or agency, branch, vehicle use, multiple coverages and package; the applicable percentages are snapshotted when a policy is issued. Commission percentages apply to net premium. The preliminary insurer remittance estimate is gross premium less the office commission calculated on net premium. See [financial links and commission settings](Software%20Analysis/12%20-%20Financial%20Links%20and%20Commission%20Settings/README.md).

The policy card now has linked holder, partner, insurer and agency; renewal numbers and dates; duration-based expiry; insured value and discounts; coverage, insured-object, installment, endorsement and green-card lists; live payment balance; linked claims; previous/next renewal navigation; and local PDF/image/DOCX attachments. See [insurance policy card](Software%20Analysis/14%20-%20Insurance%20Policy%20Card/README.md).

The latest workspace adds a customer and partner name-day calendar, renewal outreach with Brevo email and Bulker SMS configuration, filtered CSV reports, and alphabetical insurer/agency bridge folders with a standard CSV import for insurance documents. The email and SMS API keys are saved in Windows Credential Manager. Sending requires an explicit confirmation in the app; no provider credentials are included in the repository. Insurer-specific import formats, invoice mapping and live SMS credit balances require provider file samples and API configuration. See [communications, reports and bridges](Software%20Analysis/15%20-%20Communications%20Reports%20and%20Bridges/README.md).

The light theme uses soft gray surfaces throughout the workspace, card windows, filters, tables, inputs, menus, sidebar, and footer; dark theme and action colors retain their existing palette.

## Build and tests

```powershell
dotnet build AllInsurance.slnx
dotnet test tests\AllInsurance.Tests\AllInsurance.Tests.csproj
```

The integration tests use isolated temporary SQLite files and cover first-run setup, persistence, authentication, lockout, recovery, forced password changes, authorization, and last-administrator protection.

## Local security boundary

The SQLite file, backups and customer attachments are not encrypted by the app. They are stored under the current Windows user's Local AppData directory; use Windows account protections and BitLocker where available. Application permissions do not prevent someone with direct filesystem write access from tampering with local records. Do not place the database on a writable network share.
