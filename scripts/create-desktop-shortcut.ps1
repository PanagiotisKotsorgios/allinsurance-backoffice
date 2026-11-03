$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\AllInsurance.Desktop\AllInsurance.Desktop.csproj'
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'The desktop app could not be built.'
}

$output = Join-Path $repoRoot 'src\AllInsurance.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64'
$app = Join-Path $output 'AllInsurance.Desktop.exe'
$icon = Join-Path $output 'Assets\allinsurance.ico'
if (-not (Test-Path -LiteralPath $app) -or -not (Test-Path -LiteralPath $icon)) {
    throw 'The built app or icon is missing.'
}

$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'AllInsurance Backoffice Software.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $app
$shortcut.WorkingDirectory = $output
$shortcut.IconLocation = "$icon,0"
$shortcut.Description = 'AllInsurance Backoffice Software'
$shortcut.Save()
Write-Output $shortcutPath
