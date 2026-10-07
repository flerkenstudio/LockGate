param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $ProjectRoot "publish\win-x64"
$ReleaseDir = Join-Path $ProjectRoot "release"
$InstallerScript = Join-Path $ProjectRoot "installer\LockGateSetup.iss"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  LockGate Release Packager v$Version" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Clean previous publish & release folders
if (Test-Path $PublishDir) { Remove-Item -Path $PublishDir -Recurse -Force }
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null }

# 2. Publish Self-Contained Executable
Write-Host "`n[1/4] Publishing Self-Contained Windows x64 Binary..." -ForegroundColor Yellow
dotnet publish "$ProjectRoot\src\LockGate.App\LockGate.App.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

if (-not (Test-Path "$PublishDir\LockGate.exe")) {
    throw "LockGate.exe was not found in $PublishDir!"
}

# 3. Create Portable Zip Archive
Write-Host "`n[2/4] Creating Portable Zip Package..." -ForegroundColor Yellow
$ZipPath = Join-Path $ReleaseDir "LockGate-v$Version-windows-x64.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# Include executable and readme in zip
$PortableStaging = Join-Path $ReleaseDir "LockGate-v$Version-portable"
if (Test-Path $PortableStaging) { Remove-Item -Path $PortableStaging -Recurse -Force }
New-Item -ItemType Directory -Path $PortableStaging | Out-Null
Copy-Item "$PublishDir\LockGate.exe" -Destination $PortableStaging
if (Test-Path "$ProjectRoot\README.md") { Copy-Item "$ProjectRoot\README.md" -Destination $PortableStaging }

Compress-Archive -Path "$PortableStaging\*" -DestinationPath $ZipPath -Force
Remove-Item -Path $PortableStaging -Recurse -Force
Write-Host "  -> Created: $ZipPath" -ForegroundColor Green

# 4. Compile Inno Setup Installer
Write-Host "`n[3/4] Compiling Windows Setup Installer..." -ForegroundColor Yellow
$IsccPaths = @(
    "ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$Iscc = $null
foreach ($path in $IsccPaths) {
    if (Get-Command $path -ErrorAction SilentlyContinue) {
        $Iscc = (Get-Command $path).Source
        break
    }
    if (Test-Path $path) {
        $Iscc = $path
        break
    }
}

if ($Iscc) {
    Write-Host "  -> Using Inno Setup compiler: $Iscc" -ForegroundColor Gray
    & $Iscc "/DMyAppVersion=$Version" $InstallerScript
    Write-Host "  -> Installer generated successfully!" -ForegroundColor Green
} else {
    Write-Warning "Inno Setup (ISCC.exe) not found. Skipping installer exe creation. Install Inno Setup or run via GitHub Actions."
}

# 5. Generate Checksums
Write-Host "`n[4/4] Generating SHA-256 Checksums..." -ForegroundColor Yellow
$ChecksumFile = Join-Path $ReleaseDir "checksums.txt"
$Hashes = @()
Get-ChildItem -Path $ReleaseDir -Filter "LockGate-*" | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    $Hashes += "$hash  $($_.Name)"
}
$Hashes | Set-Content -Path $ChecksumFile -Encoding UTF8

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "  Packaging Complete! Release assets in:" -ForegroundColor Green
Write-Host "  $ReleaseDir" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Get-ChildItem -Path $ReleaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
