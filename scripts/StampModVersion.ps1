# Stamps the ##VERSION## placeholder in a copy of info.json with the
# version derived from the current git tag
# Outputs the stamped version for callers that need it
param(
    [string]$InfoJson = ""
)

$ErrorActionPreference = 'Stop'

if (-not $InfoJson) {
    Write-Error "Usage: StampModVersion.ps1 -InfoJson <path to info.json copy>"
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot

$ErrorActionPreference = 'Continue'
$describe = & git -C $repoRoot describe --tags 2>$null
$gitOk = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'

if (-not $gitOk -or -not $describe) {
    Write-Error "git describe --tags failed, tag the release first (e.g. git tag v0.2.0)"
    exit 1
}
$version = $describe.Trim() -replace '^v', ''

((Get-Content $InfoJson -Raw) -replace '##VERSION##', $version) |
    Set-Content -LiteralPath $InfoJson -Encoding UTF8

Write-Host "stamped version $version into $InfoJson"
$version
