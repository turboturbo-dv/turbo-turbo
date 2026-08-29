param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2019.4.40f1\Editor\Unity.exe"
)

if (-not (Test-Path $UnityPath)) {
    Write-Error "Unity not found at: $UnityPath"
    Write-Error "Install Unity 2019.4.40f1 via Unity Hub, or rerun with -UnityPath <path>"
    exit 1
}

$project = (Resolve-Path "$PSScriptRoot").Path
$logFile = Join-Path $PSScriptRoot "build.log"

& $UnityPath -batchmode -nographics -quit `
    -projectPath $project `
    -executeMethod BuildBundle.Build `
    -logFile $logFile

Write-Host "--- build finished, log: $logFile ---"
Write-Host "Bundle output: $PSScriptRoot\AssetBundles\turboturbo_assets"
Write-Host "Copy it to: <Derail Valley>\BepInEx\plugins\"
