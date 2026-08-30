# Builds the TurboTurbo mod in Release mode and deploys it to the game.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

dotnet build "$PSScriptRoot\TurboTurbo\TurboTurbo.csproj" -c $Configuration -v minimal -p:DeployMod=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host "--- built ($Configuration) and deployed ---"
