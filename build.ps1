# Builds the TurboTurbo mod and packages it for distribution.
#
# We don't build the asset bundle here because the `unity build` command doesn't
# seem to detect the licence properly.
param(
    [string]$Configuration = "Release",
    [string]$GameDir = "",
    [switch]$Install
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

$stage = "$root\dist\stage\TurboTurbo"
New-Item $stage -ItemType Directory -Force | Out-Null

$describe = git -C $root describe --tags
if ($LASTEXITCODE -ne 0 -or -not $describe) {
    Write-Error "could not find a git tag"
    exit 1
}
$version = $describe.Trim() -replace '^v', ''

Copy-Item "$root\TurboTurbo\bin\$Configuration\TurboTurbo.dll" $stage
Copy-Item "$root\TurboTurbo\info.json" $stage

$stagedInfo = Join-Path $stage "info.json"
((Get-Content $stagedInfo -Raw) -replace '##VERSION##', $version) | Set-Content -LiteralPath $stagedInfo -Encoding UTF8

$bundle = "$root\WorkBench\AssetBundles\turboturbo_assets"
if (-not (Test-Path $bundle)) {
    Write-Error "Asset bundle not found at '$bundle', build it via WorkBench: TurboTurbo/Build Bundle"
    exit 1
}
Copy-Item $bundle $stage

$zip = "$root\dist\TurboTurbo-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$root\dist\stage\TurboTurbo" -DestinationPath $zip
Remove-Item "$root\dist\stage" -Recurse -Force

Write-Host "packaged: $zip"

if ($Install) {
    # $GameDir is only set when GameDir.props was just created; otherwise read it back
    $installGameDir = $GameDir
    if (-not $installGameDir) {
        $installGameDir = ([xml](Get-Content $propsPath -Raw)).Project.PropertyGroup.GameDir
    }
    if (-not $installGameDir) {
        Write-Error "Could not determine GameDir for install. Pass it explicitly: .\build.ps1 -Install -GameDir 'C:\path\to\Derail Valley'"
        exit 1
    }

    $modsDir = Join-Path $installGameDir "Mods"
    Expand-Archive -Path $zip -DestinationPath $modsDir -Force
    Write-Host "installed to: $(Join-Path $modsDir 'TurboTurbo')"
}
