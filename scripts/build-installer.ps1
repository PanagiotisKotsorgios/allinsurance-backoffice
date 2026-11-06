param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [string]$IsccPath,
    [string]$SignToolPath,
    [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publish = (Resolve-Path -LiteralPath $PublishDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $publish 'AllInsurance.Desktop.exe'))) {
    throw 'The published application executable is missing.'
}
$databaseFiles = @(Get-ChildItem -LiteralPath $publish -Recurse -File |
    Where-Object { $_.Extension -in '.db', '.sqlite', '.sqlite3' })
if ($databaseFiles.Count -gt 0) {
    throw 'A database was found in the application package. User data must remain outside it.'
}
if (-not $IsccPath) {
    $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    if (Test-Path -LiteralPath $candidate) { $IsccPath = $candidate }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Inno Setup ISCC.exe is required to build the installer.'
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$destination = (Resolve-Path -LiteralPath $OutputDir).Path
$arguments = @(
    "/DAppVersion=$Version",
    "/DPublishDir=$publish",
    "/DInstallerOutputDir=$destination"
)
if ($CertificateThumbprint) {
    if (-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath)) {
        throw 'SignTool is required for a signed release.'
    }
    $thumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    foreach ($name in @('AllInsurance.Desktop.exe', 'AllInsurance.Desktop.dll',
            'AllInsurance.Application.dll', 'AllInsurance.Domain.dll',
            'AllInsurance.Infrastructure.dll')) {
        $binary = Join-Path $publish $name
        if (-not (Test-Path -LiteralPath $binary)) { continue }
        & $SignToolPath sign /sha1 $thumbprint /s My /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $binary
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $name." }
        if ((Get-AuthenticodeSignature -LiteralPath $binary).Status -ne 'Valid') {
            throw "The signature on $name is not trusted."
        }
    }
    $signCommand = '"' + $SignToolPath + '" sign /sha1 ' + $thumbprint +
        ' /s My /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f'
    $arguments += '/DSignedBuild=1'
    $arguments += "/Srelease=$signCommand"
} else {
    Write-Warning 'This local installer is unsigned. Do not distribute it to users.'
}
$arguments += (Join-Path $repoRoot 'installer\AllInsurance.iss')
& $IsccPath '/Q' @arguments
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $destination "AllInsurance-Backoffice-Setup-$Version-win-x64.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer output was not created.' }
if ($CertificateThumbprint -and (Get-AuthenticodeSignature -LiteralPath $setup).Status -ne 'Valid') {
    throw 'The installer signature is not trusted.'
}
Write-Output $setup
