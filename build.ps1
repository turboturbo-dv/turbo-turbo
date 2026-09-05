# Builds the TurboTurbo mod and packages it for distribution.
#
# We don't build the asset bundle here because the `unity build` command doesn't
# seem to detect the licence properly.
param(
    [string]$Configuration = "Release",
    [string]$GameDir = ""
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# The projects read the game install path from GameDir.props (gitignored);
# create it on first run so a fresh clone builds without manual setup.
$propsPath = "$root\GameDir.props"
if (-not (Test-Path -LiteralPath $propsPath)) {
    if (-not $GameDir) {
        $GameDir = & "$root\scripts\FindGameDir.ps1"
    }

    if (-not $GameDir -or -not (Test-Path -LiteralPath (Join-Path $GameDir "DerailValley_Data\Managed\Assembly-CSharp.dll"))) {
        Write-Error "Could not locate Derail Valley. Pass the install path: .\build.ps1 -GameDir 'C:\path\to\Derail Valley'"
        exit 1
    }

    @"
<Project>
  <PropertyGroup>
    <GameDir>$GameDir</GameDir>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath $propsPath -Encoding UTF8

    Write-Host "created GameDir.props: $GameDir"
}

dotnet build "$root\TurboTurbo\TurboTurbo.csproj" -c $Configuration -v minimal -p:DeployMod=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

$stage = "$root\dist\stage\Mods\TurboTurbo"
New-Item $stage -ItemType Directory -Force | Out-Null

Copy-Item "$root\TurboTurbo\bin\$Configuration\TurboTurbo.dll" $stage
Copy-Item "$root\TurboTurbo\info.json" $stage

$bundle = "$root\WorkBench\AssetBundles\turboturbo_assets"
if (-not (Test-Path $bundle)) {
    Write-Error "Asset bundle not found at '$bundle', build it via WorkBench: TurboTurbo/Build Bundle"
    exit 1
}
Copy-Item $bundle $stage

$version = (Get-Content "$root\TurboTurbo\info.json" -Raw | ConvertFrom-Json).Version
$zip = "$root\dist\TurboTurbo-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$root\dist\stage\Mods" -DestinationPath $zip
Remove-Item "$root\dist\stage" -Recurse -Force

Write-Host "packaged: $zip"
