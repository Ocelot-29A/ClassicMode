param(
    [string]$RepoDir = "g:\sts2modding\ClassicMode-beta",
    [string]$GameDir = "F:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"

function Write-Section([string]$title) {
    Write-Host ""
    Write-Host "=== $title ==="
}

function To-BoolText([bool]$v) {
    if ($v) { return "YES" }
    return "NO"
}

Write-Section "Paths"
Write-Host "RepoDir = $RepoDir"
Write-Host "GameDir = $GameDir"

$modsDir = Join-Path $GameDir "mods"
$stableDir = Join-Path $modsDir "ClassicModeMod"
$betaDir = Join-Path $modsDir "ClassicModeBetaMod"

Write-Section "Install Conflict Check"
$stableExists = Test-Path $stableDir
$betaExists = Test-Path $betaDir
Write-Host ("ClassicModeMod present: {0}" -f (To-BoolText $stableExists))
Write-Host ("ClassicModeBetaMod present: {0}" -f (To-BoolText $betaExists))
if ($stableExists -and $betaExists) {
    Write-Host "RISK: Both stable and beta ClassicMode are installed. This can cause duplicate patches/model IDs, compendium glitches, and black screen on run start."
}

Write-Section "Modifier Localization Key Coverage"
$modCs = Join-Path $RepoDir "Modifiers\ClassicCustomRunModifiers.cs"
$engJsonPath = Join-Path $RepoDir "assets\ClassicMode\localization\eng\modifiers.json"
$zhsJsonPath = Join-Path $RepoDir "assets\ClassicMode\localization\zhs\modifiers.json"

$modText = Get-Content $modCs -Raw
$requiredKeys = [regex]::Matches($modText, 'new\("modifiers",\s*"([A-Z0-9_]+)\.(title|description)"\)') |
    ForEach-Object { "{0}.{1}" -f $_.Groups[1].Value, $_.Groups[2].Value } |
    Sort-Object -Unique

$eng = Get-Content $engJsonPath -Raw | ConvertFrom-Json
$zhs = Get-Content $zhsJsonPath -Raw | ConvertFrom-Json

$engMissing = @($requiredKeys | Where-Object { -not $eng.PSObject.Properties.Name.Contains($_) })
$zhsMissing = @($requiredKeys | Where-Object { -not $zhs.PSObject.Properties.Name.Contains($_) })

Write-Host "Required keys: $($requiredKeys.Count)"
Write-Host "ENG missing: $($engMissing.Count)"
Write-Host "ZHS missing: $($zhsMissing.Count)"
if ($engMissing.Count -gt 0) { $engMissing | ForEach-Object { Write-Host "  ENG MISS: $_" } }
if ($zhsMissing.Count -gt 0) { $zhsMissing | ForEach-Object { Write-Host "  ZHS MISS: $_" } }

Write-Section "Packaged Localization Presence"
$pckSrcRoot = Join-Path $RepoDir "build\ClassicModeBeta\_pck_src"
$pckEngMod = Join-Path $pckSrcRoot "ClassicMode\localization\eng\modifiers.json"
$pckZhsMod = Join-Path $pckSrcRoot "ClassicMode\localization\zhs\modifiers.json"
Write-Host ("PCK ENG modifiers.json exists: {0}" -f (To-BoolText (Test-Path $pckEngMod)))
Write-Host ("PCK ZHS modifiers.json exists: {0}" -f (To-BoolText (Test-Path $pckZhsMod)))

Write-Section "Card Portrait Packaging Audit"
$cardsRoot = Join-Path $RepoDir "Cards"
$cardFiles = Get-ChildItem $cardsRoot -Recurse -Filter *.cs
$portraitRefs = @()
foreach ($f in $cardFiles) {
    $pool = ""
    if ($f.FullName -match '\\Cards\\Ironclad\\') { $pool = "ironclad" }
    elseif ($f.FullName -match '\\Cards\\Silent\\') { $pool = "silent" }
    elseif ($f.FullName -match '\\Cards\\Defect\\') { $pool = "defect" }
    elseif ($f.FullName -match '\\Cards\\Colorless\\') { $pool = "colorless" }

    foreach ($line in Get-Content $f.FullName) {
        if ($line -match ':\s*base\("([A-Za-z0-9_]+)"') {
            $portraitRefs += [pscustomobject]@{ Pool = $pool; Name = $matches[1] }
        }
    }
}
$portraitRefs = $portraitRefs | Where-Object { $_.Pool -ne "" } | Sort-Object Pool, Name -Unique

$cardMiss = @()
foreach ($r in $portraitRefs) {
    $normal = Join-Path $pckSrcRoot ("images\\packed\\card_portraits\\classic\\{0}\\{1}.png" -f $r.Pool, $r.Name)
    $beta = Join-Path $pckSrcRoot ("images\\packed\\card_portraits\\classic\\{0}\\beta\\{1}.png" -f $r.Pool, $r.Name)

    if (-not (Test-Path $normal)) {
        $cardMiss += [pscustomobject]@{ Pool = $r.Pool; Name = $r.Name; Missing = "normal" }
    }
    if (-not (Test-Path $beta)) {
        $cardMiss += [pscustomobject]@{ Pool = $r.Pool; Name = $r.Name; Missing = "beta" }
    }
}
Write-Host "Card portrait refs: $($portraitRefs.Count)"
Write-Host "Missing portrait files: $($cardMiss.Count)"
$cardMiss | Group-Object Pool, Missing | Sort-Object Count -Descending | ForEach-Object {
    Write-Host ("  {0} x{1}" -f $_.Name, $_.Count)
}

Write-Section "Relic Icon Packaging Audit"
$relicFiles = Get-ChildItem (Join-Path $RepoDir "Relics") -Recurse -Filter *.cs
$relicNames = @()
foreach ($f in $relicFiles) {
    foreach ($line in Get-Content $f.FullName) {
        if ($line -match ':\s*base\("([A-Za-z0-9_\-]+)"\)') {
            $relicNames += $matches[1]
        }
    }
}
$relicNames = $relicNames | Sort-Object -Unique

$relicMiss = @()
foreach ($name in $relicNames) {
    $icon = Join-Path $pckSrcRoot ("images\\relics\\classic\\{0}.png" -f $name)
    $outline = Join-Path $pckSrcRoot ("images\\relics\\classic\\outline\\{0}.png" -f $name)

    if (-not (Test-Path $icon)) {
        $relicMiss += [pscustomobject]@{ Name = $name; Missing = "icon" }
    }
    if (-not (Test-Path $outline)) {
        $relicMiss += [pscustomobject]@{ Name = $name; Missing = "outline" }
    }
}
Write-Host "Relic refs: $($relicNames.Count)"
Write-Host "Missing relic files: $($relicMiss.Count)"

Write-Section "Quick Recommendation"
if ($stableExists -and $betaExists) {
    Write-Host "1) Disable/remove ClassicModeMod and keep only ClassicModeBetaMod for beta validation."
}
if ($cardMiss.Count -gt 0) {
    Write-Host "2) Missing beta portraits are present; if beta art mode is enabled, compendium can render abnormally. Consider backfilling beta portraits or adding runtime fallback to normal portrait when beta is missing."
}
if ($engMissing.Count -eq 0 -and $zhsMissing.Count -eq 0) {
    Write-Host "3) Modifier localization keys are complete in source; if in-game still broken, prioritize runtime table-load timing or cross-mod conflicts."
}
