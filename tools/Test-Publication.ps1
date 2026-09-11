param([string]$GitleaksPath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$files = @(git -C $root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Unable to list publication files' }
# Split terms so the scanner does not introduce the retired names it prohibits.
$retiredNames = '(?i)' + 'go' + '[\s_-]*2[\s_-]*(it|io)'
$findings = @()
foreach ($file in $files) {
    if ($file -match '(?i)(^|/)(\.env[^/]*|connection\.json[^/]*|appsettings\.json)$|\.(pfx|p12|key|pem|publishsettings)$') {
        $findings += "Private configuration or key file: $file"
    }
    if ($file -match '\.(png|ico)$') { continue }
    if ([IO.File]::ReadAllText((Join-Path $root $file)) -match $retiredNames) {
        $findings += "Retired branding: $file"
    }
}
if ($findings.Count) { throw ($findings -join [Environment]::NewLine) }
Write-Host "Publication filename and legacy-reference checks passed ($($files.Count) files)."
if ($GitleaksPath) {
    # A unique snapshot excludes ignored runtime settings, SDKs and build caches.
    $snapshot = Join-Path $root ('artifacts/security/publication-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $snapshot | Out-Null
    foreach ($file in $files) {
        $destination = Join-Path $snapshot $file
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $destination
    }
    & $GitleaksPath dir $snapshot --redact --no-banner
    if ($LASTEXITCODE -ne 0) { throw 'Gitleaks scan failed' }
}
