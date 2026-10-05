param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$DistDir = Join-Path $Root "dist\FivePRS"
$ZipOut  = Join-Path $Root "dist\FivePRS.zip"

function Info($msg) { Write-Host "[FivePRS] $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "[FivePRS] $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "[FivePRS] WARNING: $msg" -ForegroundColor Yellow }

# 1. Clean dist
Info "Cleaning previous dist..."
if (Test-Path (Join-Path $Root "dist")) { Remove-Item -Recurse -Force (Join-Path $Root "dist") }
$null = New-Item -ItemType Directory -Path "$DistDir\client"
$null = New-Item -ItemType Directory -Path "$DistDir\server"
$null = New-Item -ItemType Directory -Path "$DistDir\plugins"
$null = New-Item -ItemType Directory -Path "$DistDir\callouts"

# 2. Build
Info "Building ($Configuration)..."
dotnet build "$Root\FivePRS.sln" -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

# 3. Copy fxmanifest + config
Copy-Item "$Root\fxmanifest.lua" "$DistDir\fxmanifest.lua"

Info "Copying config files..."
if (Test-Path "$Root\config") {
    Copy-Item -Recurse "$Root\config" "$DistDir\config"
} else {
    Warn "config/ folder not found — server will use built-in defaults."
}

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
"  - Drop the output DLL here and restart FivePRS"
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
"  - Drop the output DLL here and restart FivePRS - no core recompile needed"
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

# 8. Zip the dist folder
Info "Creating zip archive..."
if (Test-Path $ZipOut) { Remove-Item $ZipOut -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($DistDir, $ZipOut)
Ok "Zip created -> $ZipOut"

# 9. Summary
$fileCount = (Get-ChildItem -Recurse -File $DistDir).Count
$bytes = (Get-ChildItem -Recurse -File $DistDir | Measure-Object -Property Length -Sum).Sum
$distMB = [math]::Round($bytes / 1MB, 2)
$zipMB  = [math]::Round((Get-Item $ZipOut).Length / 1MB, 2)

Write-Host ""
Ok "Published -> $DistDir ($fileCount files, ~${distMB} MB)"
Ok "Zipped   -> $ZipOut (~${zipMB} MB)"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Extract FivePRS.zip into your server resources\ directory." -ForegroundColor Yellow
Write-Host "  2. Add to server.cfg:" -ForegroundColor Yellow
Write-Host '       set fiveprs_db_type      "sqlite"   # or "mysql"' -ForegroundColor Gray
Write-Host '       set fiveprs_db_connection ""         # MySQL only' -ForegroundColor Gray
Write-Host '       set fiveprs_restrict_departments "false" # "true" to require ACE per department' -ForegroundColor Gray
Write-Host "       ensure FivePRS" -ForegroundColor Gray
Write-Host ""
Write-Host "  plugins\   <- drop functionality extensions here" -ForegroundColor Yellow
Write-Host "  callouts\  <- drop scenario packs here" -ForegroundColor Yellow
Write-Host "  (restart the resource after adding any DLL - no recompile needed)" -ForegroundColor Yellow
Write-Host ""
