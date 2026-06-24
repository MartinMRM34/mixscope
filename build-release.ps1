# build-release.ps1
# Produces a self-contained, single-file release of the overlay with MediaInfo bundled,
# plus a zip ready to attach to a GitHub release.
#
#   ./build-release.ps1 -Version 0.1.0

param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj  = Join-Path $root "mixscope.csproj"
$dist  = Join-Path $root "dist"
$stage = Join-Path $dist "mixscope"
$zip   = Join-Path $dist "mixscope-$Version-win-x64.zip"

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Publishing self-contained single-file exe (win-x64)..." -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -p:Version=$Version -o $stage | Out-Null

# Bundle MediaInfo CLI next to the exe so the package is self-sufficient.
$mi = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter MediaInfo.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\Links\\' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $mi) { $mi = (Get-Command MediaInfo -ErrorAction SilentlyContinue).Source }
if ($mi) {
    Copy-Item $mi (Join-Path $stage "MediaInfo.exe") -Force
    Write-Host "Bundled MediaInfo: $mi" -ForegroundColor Green
} else {
    Write-Warning "MediaInfo.exe not found. Install it (winget install MediaArea.MediaInfo) and re-run, or users must install it themselves."
}

# Docs + helper script
foreach ($f in "README.md", "setup-vlc.ps1", "LICENSE") {
    $src = Join-Path $root $f
    if (Test-Path $src) { Copy-Item $src $stage -Force }
}

# Trim stray symbols
Get-ChildItem $stage -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force

Write-Host "`nPackage contents:" -ForegroundColor Cyan
Get-ChildItem $stage | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize
Write-Host "Zip: $zip" -ForegroundColor Green
