$ErrorActionPreference = "Stop"

$modName = "ClassicMode"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $projectDir "..\..\")).Path
. (Join-Path $repoRoot "_tools\mod_build_common.ps1")

$project = Join-Path $projectDir "$modName.csproj"
$outputDir = Join-Path $repoRoot "mods\$modName"
$buildDir = Join-Path $projectDir "bin\Release\net9.0"
$pckRoot = Join-Path $outputDir "_pck_src"
$assetSourceDir = Join-Path $projectDir "assets"
$pckPath = Join-Path $outputDir "$modName.pck"

Ensure-Dir $outputDir
Ensure-Dir $pckRoot

# Step 1: Build DLL
Write-Host "Building $modName..."
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) {
  throw "dotnet build failed with exit code $LASTEXITCODE"
}

Copy-Item (Join-Path $buildDir "*") $outputDir -Recurse -Force

# Manifest: <id>.json to output dir (0.99+) + inside PCK (0.98.x compat)
Copy-Item (Join-Path $projectDir "mod_manifest.json") (Join-Path $outputDir "$modName.json") -Force
Copy-Item (Join-Path $projectDir "mod_manifest.json") (Join-Path $pckRoot "mod_manifest.json") -Force
Copy-IfExists (Join-Path $projectDir "README.md") (Join-Path $outputDir "README.md")

# Step 2: Prepare assets (copy STS1 portraits, relics, localization)
Write-Host "Preparing assets..."
python (Join-Path $projectDir "prepare_assets.py") $projectDir $pckRoot
if ($LASTEXITCODE -ne 0) {
  throw "prepare_assets.py failed with exit code $LASTEXITCODE"
}

# Step 3: Godot texture import (skips if no images or no Godot)
Import-GodotTextures -PckRoot $pckRoot -ModName $modName -RepoRoot $repoRoot

# Step 4: Pack PCK + ZIPs
Build-ModPck -PckRoot $pckRoot -PckPath $pckPath -RepoRoot $repoRoot
Package-ModZips -OutputDir $outputDir -ModName $modName -PckPath $pckPath -RepoRoot $repoRoot -ProjectDir $projectDir
