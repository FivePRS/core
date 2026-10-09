param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$Root      = $PSScriptRoot
$DistRoot  = Join-Path $Root "dist"
$DistDir   = Join-Path $DistRoot "fiveprs"
$CoreZip   = Join-Path $DistRoot "fiveprs.zip"
$AddonsDir = Join-Path $DistRoot "[fiveprs_addons]"
$AddonsZip = Join-Path $DistRoot "fiveprs_addons.zip"

function Info($msg) { Write-Host "[FivePRS] $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "[FivePRS] $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "[FivePRS] WARNING: $msg" -ForegroundColor Yellow }

# 1. Clean dist
Info "Cleaning previous dist..."
if (Test-Path -LiteralPath $DistRoot) { Remove-Item -LiteralPath $DistRoot -Recurse -Force }
$null = New-Item -ItemType Directory -Path "$DistDir\client"
$null = New-Item -ItemType Directory -Path "$DistDir\server"
$null = New-Item -ItemType Directory -Path "$DistDir\plugins"
$null = New-Item -ItemType Directory -Path "$DistDir\callouts"

# 2. Build
Info "Building ($Configuration)..."
dotnet build "$Root\FivePRS.sln" -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

# 3. Copy fxmanifest, license, readme + config
Copy-Item "$Root\fxmanifest.lua" "$DistDir\fxmanifest.lua"
Copy-Item "$Root\LICENSE" "$DistDir\LICENSE"
Copy-Item "$Root\README.md" "$DistDir\README.md"

Info "Copying config files..."
if (Test-Path "$Root\config") {
    Copy-Item -Recurse "$Root\config" "$DistDir\config"
} else {
    Warn "config/ folder not found — server will use built-in defaults."
}

Info "Copying NUI files..."
Copy-Item -Recurse "$Root\nui" "$DistDir\nui"

# 4. Copy client DLLs  (bin\client -> client)
# CitizenFX.* is injected by FiveM at runtime - do NOT bundle it.
Info "Copying client binaries..."
Get-ChildItem "$Root\bin\client" -File | Where-Object {
    $_.Extension -eq ".dll" -and
    $_.Name -notlike "CitizenFX.*"
} | Copy-Item -Destination "$DistDir\client"

# 5. Copy server DLLs  (bin\server -> server)
# Native SQLite (e_sqlite3.dll on Windows, libe_sqlite3.so on Linux) sits next to the DLLs.
Info "Copying server binaries..."
Get-ChildItem "$Root\bin\server" -File | Where-Object {
    ($_.Extension -eq ".dll" -or $_.Extension -eq ".so") -and
    $_.Name -notlike "CitizenFX.*"
} | Copy-Item -Destination "$DistDir\server"

# 6. Drop READMEs into plugins/ and callouts/
@(
"FivePRS Plugins Folder",
"========================",
"",
"Drop functionality-extension DLLs here.",
"",
"Examples of what belongs here:",
"  - New agency modules (EMS, Fire, etc.)",
"  - Custom command packs",
"  - HUD / NUI overlay systems",
"  - Additional BaseScript modules",
"",
"Building a plugin:",
"  - Reference client\FivePRS.Core.dll and client\FivePRS.Client.net.dll",
"  - Subclass BaseScript for any systems you need",
"  - Build as a net452 Class Library named <Name>.net.dll",
"  - Drop the output DLL here and restart fiveprs"
) | Set-Content "$DistDir\plugins\README.txt" -Encoding ASCII

@(
"FivePRS Callouts Folder",
"=========================",
"",
"Drop scenario DLLs here. Each DLL is scanned on startup for",
"[CalloutInfo]-decorated CalloutBase subclasses, which are then",
"registered with the dispatcher automatically.",
"",
"Examples of what belongs here:",
"  - Custom police callouts (traffic stop, bank robbery, etc.)",
"  - Department-specific scenarios",
"",
"Building a callout pack:",
"  - Reference client\FivePRS.Core.dll and client\FivePRS.Client.net.dll",
"  - Subclass CalloutBase and annotate with [CalloutInfo(Name, Dept, Weight)]",
"  - Build as a net452 Class Library named <Name>.net.dll",
"  - Drop the output DLL here and restart fiveprs - no core recompile needed"
) | Set-Content "$DistDir\callouts\README.txt" -Encoding ASCII

# 7. Verify fxmanifest references (skip comments and wildcard globs)
Info "Verifying manifest references..."
$missing = @()
$activeLines = Get-Content "$DistDir\fxmanifest.lua" | Where-Object { $_ -notmatch "^\s*--" }
$manifestContent = $activeLines -join "`n"
$refs = [regex]::Matches($manifestContent, "(client|server)/[^'*\n]+\.dll") |
    ForEach-Object { $_.Value -replace "/", "\" }
foreach ($ref in $refs) {
    $full = Join-Path $DistDir $ref
    if (-not (Test-Path $full)) { $missing += $ref }
}
if ($missing.Count -gt 0) {
    Warn "Files referenced in fxmanifest.lua but missing from dist:"
    $missing | ForEach-Object { Write-Host "   MISSING: $_" -ForegroundColor Red }
} else {
    Ok "All manifest references satisfied."
}

# 8. Copy addon resources (every addons\<name>\ with an fxmanifest.lua)
Info "Copying addon resources..."
$null = New-Item -ItemType Directory -Path $AddonsDir
$addons = @(Get-ChildItem -LiteralPath (Join-Path $Root "addons") -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "fxmanifest.lua") })
foreach ($addon in $addons) {
    $addonDest = Join-Path $AddonsDir $addon.Name
    Copy-Item -LiteralPath $addon.FullName -Destination $addonDest -Recurse
    if (-not (Test-Path -LiteralPath (Join-Path $addonDest "LICENSE"))) {
        Copy-Item -LiteralPath (Join-Path $Root "LICENSE") -Destination (Join-Path $addonDest "LICENSE")
    }
}
if ($addons.Count -eq 0) { Warn "No addons found under addons\." }

# 9. Zip both bundles (each zip contains its top-level folder)
Info "Creating zip archives..."
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ResourceZip($sourceDir, $zipPath) {
    $source = (Get-Item -LiteralPath $sourceDir).FullName
    $prefix = Split-Path $source -Leaf
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
            $entry = $prefix + "/" + $file.FullName.Substring($source.Length + 1).Replace("\", "/")
            $null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally {
        $zip.Dispose()
    }
}

New-ResourceZip $DistDir $CoreZip
New-ResourceZip $AddonsDir $AddonsZip

# 10. Summary
$fileCount = (Get-ChildItem -LiteralPath $DistDir -Recurse -File).Count
$coreMB    = [math]::Round((Get-Item -LiteralPath $CoreZip).Length / 1MB, 2)
$addonsMB  = [math]::Round((Get-Item -LiteralPath $AddonsZip).Length / 1MB, 2)

Write-Host ""
Ok "Core   -> $DistDir ($fileCount files)"
Ok "Zipped -> $CoreZip (~${coreMB} MB)"
Ok "Addons -> $AddonsDir ($($addons.Count): $(($addons | ForEach-Object Name) -join ', '))"
Ok "Zipped -> $AddonsZip (~${addonsMB} MB)"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Extract fiveprs.zip (and optionally fiveprs_addons.zip) into your server resources\ directory." -ForegroundColor Yellow
Write-Host "  2. Add to server.cfg:" -ForegroundColor Yellow
Write-Host '       set fiveprs_db_type      "sqlite"   # or "mysql"' -ForegroundColor Gray
Write-Host '       set fiveprs_restrict_departments "false" # "true" to require ACE per department' -ForegroundColor Gray
Write-Host "       ensure fiveprs" -ForegroundColor Gray
foreach ($addon in $addons) {
    Write-Host "       ensure $($addon.Name)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "  plugins\   <- drop functionality extensions here" -ForegroundColor Yellow
Write-Host "  callouts\  <- drop scenario packs here" -ForegroundColor Yellow
Write-Host "  (restart the resource after adding any DLL - no recompile needed)" -ForegroundColor Yellow
Write-Host ""
