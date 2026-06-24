# setup-vlc.ps1
# Enables VLC's Web (Lua HTTP) interface so the overlay can read what VLC is playing.
# Safe + reversible: it only flips a few keys in your vlcrc. VLC must be closed while we edit.

param(
    [string]$Password = "overlay123",
    [string]$HostAddr = "127.0.0.1",
    [int]$Port = 8080
)

$vlcrc = Join-Path $env:APPDATA "vlc\vlcrc"

Write-Host "VLC config: $vlcrc"

# VLC rewrites vlcrc on exit, so it must be closed before we edit.
$running = Get-Process vlc -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Closing VLC ($($running.Count) instance(s))..."
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 600
}

if (-not (Test-Path $vlcrc)) {
    Write-Warning "vlcrc not found. Open VLC once (then close it) so it creates the file, and re-run this script."
    exit 1
}

$content = Get-Content $vlcrc -Raw

function Set-VlcKey([string]$text, [string]$key, [string]$value) {
    # vlcrc ships every option present-but-commented under its correct [section],
    # so replacing the existing (commented) line keeps it in the right section.
    $pat = "(?m)^#?\s*$([regex]::Escape($key))=.*$"
    if ([regex]::IsMatch($text, $pat)) {
        return [regex]::Replace($text, $pat, "$key=$value")
    }
    Write-Warning "Key '$key' not found in vlcrc; leaving unchanged."
    return $text
}

$content = Set-VlcKey $content "extraintf"     "http"
$content = Set-VlcKey $content "http-host"     $HostAddr
$content = Set-VlcKey $content "http-port"     "$Port"
$content = Set-VlcKey $content "http-password" $Password

# Write UTF-8 without BOM (VLC dislikes a BOM).
[System.IO.File]::WriteAllText($vlcrc, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Done. VLC Web interface enabled:" -ForegroundColor Green
Write-Host "  http://$HostAddr`:$Port/   (user blank, password '$Password')"
Write-Host ""
Write-Host "Make sure the overlay's config.json matches (VlcPort=$Port, VlcPassword='$Password')."
Write-Host "Now open VLC, play a file, and the overlay will show its audio format."
