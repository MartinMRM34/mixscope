# Audio Channel Overlay

A small always-on-top Windows 11 gadget that shows the **exact audio format of whatever
VLC is currently playing** — including **Dolby Atmos**, **DTS:X**, TrueHD, DTS-HD MA,
Dolby Digital+, AAC, PCM, channel layout (2.0 / 5.1 / 7.1), sample rate and bitrate.

It mirrors the kind of "now playing format" readout a Sony Bravia / AV receiver shows.

## Why it works this way

Atmos / 5.1 / stereo is metadata that only exists at the **decoder / bitstream** level.
Windows' audio mixer sits *after* decoding — by the time audio reaches the output device
it is downmixed PCM, so the OS can't tell Atmos from 5.1 from stereo. The reliable way to
know the real format is to read it at the source. This app does that in two steps:

1. Ask **VLC** (via its Web interface) which file + position is playing.
2. Probe that file with **MediaInfo**, which reads the actual stream flags
   (`Format_Commercial_IfAny = "...Dolby Atmos"`, `Format_AdditionalFeatures = "JOC"`,
   `DTS:X`, etc.) — a 100%-accurate read of the content format.

> Scope: this covers anything you play **in VLC**. It cannot read browser streaming or
> Microsoft Store streaming apps (Netflix app, Disney+ app) — those expose nothing to the OS.

## Install (end users)

Grab the latest [release](../../releases):

- **`AudioChannelOverlay-Setup-*.exe`** — installer (Start Menu entry, optional auto-start, uninstaller). No admin needed.
- **`AudioChannelOverlay-*-win-x64.zip`** — portable. Unzip and run `AudioChannelOverlay.exe`; right-click the bar → **Start with Windows** to auto-launch.

Both are **self-contained** (the .NET runtime and MediaInfo are bundled — nothing else to install).
One-time step: enable VLC's Web interface by running `setup-vlc.ps1` (included).

## Requirements (to build from source)

- **.NET 8 SDK**.
- **VLC** with the Web (Lua HTTP) interface enabled — run `setup-vlc.ps1`.
- **MediaInfo CLI** — `winget install MediaArea.MediaInfo` (bundled into release builds automatically).

## Setup

```powershell
# 1. Enable VLC's Web interface (closes VLC, flips a few vlcrc keys, reversible)
./setup-vlc.ps1                       # defaults: port 8080, password "overlay123"
# or customize:  ./setup-vlc.ps1 -Password "yourpass" -Port 8080

# 2. Build
dotnet build AudioChannelOverlay.csproj -c Release

# 3. Run
./bin/Release/net8.0-windows/AudioChannelOverlay.exe
```

Then open VLC, play something, and the overlay updates within ~1 second.

## Configuration

Settings live in `%APPDATA%\AudioChannelOverlay\config.json`:

| Key | Meaning | Default |
|-----|---------|---------|
| `VlcHost` / `VlcPort` | VLC Web interface address | `127.0.0.1` / `8080` |
| `VlcPassword` | Password set in VLC's Web interface | `""` |
| `PollMs` | How often to poll VLC (ms) | `1000` |
| `MediaInfoPath` | Explicit MediaInfo.exe (auto-detected if null) | `null` |
| `Anchor` | Screen anchor: `TopLeft` / `TopCenter` / `TopRight` / `BottomLeft` / `BottomCenter` / `BottomRight` / `Custom` | `TopRight` |
| `Left` / `Top` | Saved position, used only when `Anchor` is `Custom` | `null` |

The password and port **must match** what `setup-vlc.ps1` configured in VLC.

## Controls

- **Right-click → Position** to pin it to any corner/edge (top-left/center/right, bottom-left/center/right).
- **Drag** anywhere to place it freely (becomes a `Custom` position, remembered).
- **Hover** to see the filename as a tooltip.
- **Right-click → Exit** to close.

## Tied to VLC (lifecycle)

The overlay is a lightweight background companion for VLC:

- It **auto-starts at login** (a shortcut is installed in
  `shell:startup` → `Audio Channel Overlay.lnk`, pointing at the Release `.exe`).
- It stays **completely invisible while VLC is closed** (no window, no taskbar entry —
  just a tiny resident process that checks for `vlc.exe`).
- The bar **appears the moment VLC opens** and **disappears when VLC closes**.

So you never launch or close it manually — open VLC and it's there; close VLC and it's gone.

To stop it entirely / disable auto-start: delete the `Audio Channel Overlay.lnk`
shortcut from the Startup folder (and end the running process once via Task Manager,
or right-click the bar → Exit while VLC is open).

A rolling one-line status is written to
`%APPDATA%\AudioChannelOverlay\status.txt` for troubleshooting.

## How the format badge is colored

| Badge | Meaning |
|-------|---------|
| **DOLBY ATMOS** (gold) | Atmos detected (JOC in E-AC-3, or TrueHD+Atmos) |
| **DTS:X** (blue) | DTS:X objects |
| green | lossless surround (TrueHD, DTS-HD MA, PCM multichannel) |
| teal | lossy surround (DD+, DD, DTS core) |
| grey | stereo / mono |
