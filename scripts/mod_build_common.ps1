# ============================================================================
# Shared build functions for STS2 mods (standalone-repo edition)
# Usage: dot-source from your mod's build.ps1:
#   . (Join-Path $PSScriptRoot "scripts\mod_build_common.ps1")
#
# Required variables (set before dot-sourcing):
#   $modName     - Mod name (e.g. "ClassicMode")
#   $projectDir  - Path to mod source directory
#   $gameDir     - Path to STS2 game root (contains data_sts2_windows_x86_64/)
#
# This file expects its sibling python scripts (pack_godot_pck.py,
# import_assets.py) to live next to it in the same scripts/ directory.
# ============================================================================

# Directory this script lives in — used to find sibling python helpers.
$script:ModBuildCommonDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Ensure-Dir {
  param([string]$Path)
  New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Copy-IfExists {
  param(
    [string]$Source,
    [string]$Destination
  )
  if (Test-Path $Source) {
    Ensure-Dir (Split-Path -Parent $Destination)
    Copy-Item $Source $Destination -Force
  }
}

# ---------------------------------------------------------------------------
# Find-GodotBin: locate the Godot editor binary
# Returns path or $null
# ---------------------------------------------------------------------------
function Find-GodotBin {
  if ($env:GODOT_BIN -and (Test-Path $env:GODOT_BIN)) {
    return $env:GODOT_BIN
  }

  $candidates = @()

  # Check PATH
  $inPath = Get-Command "godot" -ErrorAction SilentlyContinue
  if ($inPath) { $candidates += $inPath.Source }
  $inPath = Get-Command "godot.exe" -ErrorAction SilentlyContinue
  if ($inPath) { $candidates += $inPath.Source }

  # Common install locations (newest first)
  $candidates += @(
    "D:\software\godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe",
    "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe",
    "C:\Godot\Godot_v4.5-stable_mono_win64.exe",
    "C:\Godot\Godot_v4.4-stable_mono_win64.exe",
    "$env:LOCALAPPDATA\Godot\Godot_v4.5.1-stable_mono_win64.exe",
    "$env:LOCALAPPDATA\Godot\Godot_v4.5-stable_mono_win64.exe"
  )

  foreach ($c in $candidates) {
    if (Test-Path $c) { return $c }
  }
  return $null
}

# ---------------------------------------------------------------------------
# Import-GodotTextures: convert raw PNGs to .ctex via Godot headless import
#
# Raw PNGs in a PCK can't be loaded by ResourceLoader on Android.
# This function runs Godot's headless import to convert them to .ctex,
# then relocates the imported files out of .godot/ to avoid conflicts
# with the host game's own .godot/ directory.
#
# Safe to call unconditionally — skips if no images or no Godot found.
# ---------------------------------------------------------------------------
function Import-GodotTextures {
  param(
    [string]$PckRoot,
    [string]$ModName,
    [string]$RepoRoot = $script:ModBuildCommonDir
  )

  $imageFiles = Get-ChildItem -Path $PckRoot -Include "*.png","*.jpg","*.jpeg" -Recurse -File -ErrorAction SilentlyContinue
  $spineFiles = Get-ChildItem -Path $PckRoot -Include "*.atlas","*.skel" -Recurse -File -ErrorAction SilentlyContinue
  $allImportable = @()
  if ($imageFiles) { $allImportable += $imageFiles }
  if ($spineFiles) { $allImportable += $spineFiles }
  if (-not $allImportable) {
    Write-Host "No importable assets found in PCK source, skipping Godot import."
    return
  }

  Write-Host ""
  $imgCount = if ($imageFiles) { $imageFiles.Count } else { 0 }
  $spnCount = if ($spineFiles) { $spineFiles.Count } else { 0 }
  Write-Host "Found $imgCount image(s) + $spnCount Spine file(s) to import via Godot..."

  $godotBin = Find-GodotBin
  if (-not $godotBin) {
    Write-Host "WARNING: Godot editor not found. Skipping texture import."
    Write-Host "  Set GODOT_BIN env var or install Godot to a standard location."
    Write-Host "  Raw PNGs will be packed as-is (won't load on Android)."
    return
  }

  Write-Host "Using Godot: $godotBin"

  # Create minimal project.godot so Godot recognizes _pck_src as a project
  $projectGodot = Join-Path $PckRoot "project.godot"
  if (-not (Test-Path $projectGodot)) {
    Set-Content -Path $projectGodot -Value @"
; Temporary project for Godot texture import
[gd_resource type="ProjectSettings" format=3]
config_version=5

[application]
config/name="$ModName Asset Import"
"@
  }

  # Run Godot headless import
  Write-Host "Running Godot headless import..."
  & $godotBin --headless --path $PckRoot --import 2>&1 | ForEach-Object { Write-Host "  $_" }
  if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Godot headless import exited with code $LASTEXITCODE"
  }

  # Relocate .ctex files from .godot/imported/ to <ModName>/_imported/
  $importScript = Join-Path $script:ModBuildCommonDir "import_assets.py"
  python $importScript $PckRoot $ModName
  if ($LASTEXITCODE -ne 0) {
    throw "import_assets.py failed with exit code $LASTEXITCODE"
  }
}

# ---------------------------------------------------------------------------
# Build-ModPck: pack _pck_src into a .pck file
# ---------------------------------------------------------------------------
function Build-ModPck {
  param(
    [string]$PckRoot,
    [string]$PckPath,
    [string]$RepoRoot = $script:ModBuildCommonDir
  )

  Write-Host "Packing PCK..."
  python (Join-Path $script:ModBuildCommonDir "pack_godot_pck.py") `
    $PckRoot `
    -o $PckPath `
    --engine-version 4.5.1 `
    --pack-version 3
  if ($LASTEXITCODE -ne 0) {
    throw "pack_godot_pck.py failed with exit code $LASTEXITCODE"
  }

  # Remove mod_manifest.json from _pck_src after packing —
  # if left on disk, the game scans it as a separate mod and fails
  $pckManifest = Join-Path $PckRoot "mod_manifest.json"
  if (Test-Path $pckManifest) { Remove-Item $pckManifest -Force }
}

# ---------------------------------------------------------------------------
# Package-ModZips: create versioned ZIP archives for 0.98.x and 0.99+
# ---------------------------------------------------------------------------
function Package-ModZips {
  param(
    [string]$OutputDir,
    [string]$ModName,
    [string]$PckPath,
    [string]$RepoRoot,
    [string]$ProjectDir
  )

  $version = (Get-Content -Raw -Encoding UTF8 (Join-Path $ProjectDir "mod_manifest.json") | ConvertFrom-Json).version
  $zipDir = Split-Path -Parent $OutputDir
  $zip098 = Join-Path $zipDir "${ModName}-STS2_0.98.x-${version}.zip"
  $zip099 = Join-Path $zipDir "${ModName}-STS2_0.99-${version}.zip"
  if (Test-Path $zip098) { Remove-Item $zip098 -Force }
  if (Test-Path $zip099) { Remove-Item $zip099 -Force }
  # 0.98.x: DLL + PCK (mod_manifest.json embedded in PCK)
  Compress-Archive -Path (Join-Path $OutputDir "$ModName.dll"), $PckPath -DestinationPath $zip098
  # 0.99+: DLL + <id>.json + PCK
  Compress-Archive -Path (Join-Path $OutputDir "$ModName.dll"), (Join-Path $OutputDir "$ModName.json"), $PckPath -DestinationPath $zip099

  Write-Host ""
  Write-Host "Build complete:"
  Write-Host "  DLL: $OutputDir\$ModName.dll"
  Write-Host "  PCK: $PckPath"
  Write-Host "  ZIP (0.98.x): $zip098"
  Write-Host "  ZIP (0.99+):  $zip099"
}
