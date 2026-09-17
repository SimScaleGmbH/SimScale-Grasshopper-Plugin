<#
.SYNOPSIS
    Stages the built plugin and its dependencies into a Yak package and builds the .yak file.

.DESCRIPTION
    Yak expects manifest.yml to sit in the same folder as the plugin assembly (.gha) and
    all of its dependency .dlls (Grasshopper resolves dependencies from that folder at
    load time, so nothing can live in a subfolder). This script copies everything from
    latest_stable/SimScale/src — the already-built plugin output committed to this repo —
    plus yak_package/manifest.yml (and icon.png, if present) into a scratch staging
    folder, then runs `yak build` there.

.PARAMETER YakExe
    Path to the yak CLI. Defaults to "yak.exe" (i.e. assumes it's on PATH). Grab it from
    your own Rhino install ("C:\Program Files\Rhino 8\System\Yak.exe") or download the
    standalone tool from https://files.mcneel.com/yak/tools/latest/yak.exe.

.PARAMETER Platform
    Target platform passed to `yak build --platform`. This plugin is Windows-only, so
    the default is "win".
#>
param(
    [string]$YakExe = "yak.exe",
    [string]$Platform = "win"
)

$ErrorActionPreference = "Stop"

$repoRoot    = Split-Path -Parent $PSScriptRoot
$sourceDir   = Join-Path $repoRoot "latest_stable/SimScale/src"
$packageDir  = Join-Path $repoRoot "yak_package"
$stagingDir  = Join-Path $repoRoot "build/yak_staging"

if (-not (Test-Path $sourceDir)) {
    throw "Built plugin output not found at $sourceDir — build the .gha first (see External Building Aerodynamics.sln)."
}

if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Write-Host "Staging manifest..."
Copy-Item (Join-Path $packageDir "manifest.yml") $stagingDir

$iconPath = Join-Path $packageDir "icon.png"
if (Test-Path $iconPath) {
    Copy-Item $iconPath $stagingDir
} else {
    Write-Warning "No icon.png found in yak_package/ — publishing without an icon."
}

Write-Host "Staging plugin + dependencies from $sourceDir..."
Copy-Item (Join-Path $sourceDir "*") $stagingDir -Recurse -Force

Push-Location $stagingDir
try {
    & $YakExe build --platform $Platform
    if ($LASTEXITCODE -ne 0) {
        throw "yak build failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

$yakFile = Get-ChildItem -Path $stagingDir -Filter "*.yak" | Select-Object -First 1
if (-not $yakFile) {
    throw "yak build did not produce a .yak file"
}

Copy-Item $yakFile.FullName $repoRoot -Force
Write-Host "Built package: $($yakFile.Name)"
Write-Host "Next: yak push `"$($yakFile.Name)`"  (requires `yak login` first — see yak_package/README.md)"
