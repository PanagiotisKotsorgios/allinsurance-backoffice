#ifndef AppVersion
  #error AppVersion must be supplied by the build script.
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by the build script.
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir must be supplied by the build script.
#endif

[Setup]
AppId=CoverageOfferSolutions.AllInsuranceBackoffice
AppName=AllInsurance Backoffice Software
AppVersion={#AppVersion}
AppPublisher=Coverage Offer Solutions
DefaultDirName={localappdata}\Programs\AllInsurance Backoffice
DefaultGroupName=AllInsurance Backoffice
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
MinVersion=10.0.19041
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\AllInsurance.Desktop\Assets\allinsurance.ico
UninstallDisplayIcon={app}\AllInsurance.Desktop.exe
OutputDir={#InstallerOutputDir}
OutputBaseFilename=AllInsurance-Backoffice-Setup-{#AppVersion}-win-x64
VersionInfoVersion={#AppVersion}
CloseApplications=yes
RestartApplications=no
#ifdef SignedBuild
SignTool=release
SignedUninstaller=yes
#endif

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.db,*.db-shm,*.db-wal,*.sqlite,*.sqlite3"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AllInsurance Backoffice"; Filename: "{app}\AllInsurance.Desktop.exe"
Name: "{autodesktop}\AllInsurance Backoffice"; Filename: "{app}\AllInsurance.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AllInsurance.Desktop.exe"; Description: "Launch AllInsurance Backoffice"; Flags: nowait postinstall skipifsilent
