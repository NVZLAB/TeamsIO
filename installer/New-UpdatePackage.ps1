param(
    [string]$ReleaseNotesPath = (Join-Path $PSScriptRoot '..\RELEASE_NOTES.md')
)

$ErrorActionPreference = 'Stop'
$outputDirectory = Join-Path $PSScriptRoot 'Output'
$publishExecutable = Join-Path $PSScriptRoot `
    '..\bin\Release\net8.0-windows\win-x64\publish\TeamsIO.exe'
$fileVersion = (Get-Item -LiteralPath $publishExecutable).VersionInfo.FileVersion

if ($fileVersion -notmatch '^(\d+\.\d+\.\d+)') {
    throw "Could not determine the published TeamsIO version."
}

$version = $Matches[1]
$installer = Get-Item -LiteralPath (
    Join-Path $outputDirectory "TeamsIO_Setup_$version.exe")
if ($installer.VersionInfo.ProductVersion.Trim() -notmatch ('^' + [regex]::Escape($version) + '(\.0)?$')) {
    throw 'Installer metadata does not match the published application version. Recompile Setup first.'
}
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash
$notesDestination = Join-Path $outputDirectory 'release-notes.md'
$allReleaseNotes = Get-Content -LiteralPath $ReleaseNotesPath -Raw -Encoding UTF8
$currentReleaseNotes = ($allReleaseNotes -split '(?m)^---\s*$', 2)[0].Trim()
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    $notesDestination,
    $currentReleaseNotes + [Environment]::NewLine,
    $utf8WithoutBom)

$manifest = [ordered]@{
    version = $version
    installerFileName = $installer.Name
    sha256 = $hash
    releaseNotesFileName = 'release-notes.md'
}

$manifestJson = $manifest | ConvertTo-Json
[System.IO.File]::WriteAllText(
    (Join-Path $outputDirectory 'update.json'),
    $manifestJson,
    $utf8WithoutBom)

Write-Host "Created GitHub Releases update package for TeamsIO $version"
Write-Host "SHA-256: $hash"
