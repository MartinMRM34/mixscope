# mixscope

[![Release](https://img.shields.io/github/v/release/MartinMRM34/mixscope?sort=semver)](https://github.com/MartinMRM34/mixscope/releases)
[![Downloads](https://img.shields.io/github/downloads/MartinMRM34/mixscope/total)](https://github.com/MartinMRM34/mixscope/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6)

A small always-on-top Windows overlay that shows the **real audio format of whatever
[VLC](https://www.videolan.org/) is playing** — Dolby Atmos, DTS:X, TrueHD, DTS-HD MA,
Dolby Digital+, AAC, PCM — with channel layout (2.0 / 5.1 / 7.1), sample rate and
lossless/lossy, in a thin NVIDIA-style strip.

```
●  DOLBY ATMOS │ TrueHD │ 7.1 │ 48 kHz │ lossless
●  DTS:X │ DTS │ 7.1 │ 48 kHz │ lossless
●  Dolby Digital+ │ 5.1 │ 48 kHz │ lossy
●  AAC │ 2.0 │ 48 kHz │ lossy
```

It mirrors the kind of "now playing format" readout a Sony Bravia / AV receiver shows — but on
your PC. The format is read from the actual stream flags, so it's exact (not guessed from the OS
mixer, which can't tell Atmos from 5.1 from stereo).

> ⚠️ **Works with VLC only.** It reads VLC's web interface to know what's playing. It does **not**
> work with browsers (Netflix/Prime in a tab), Microsoft Store streaming apps, or other media
> players — those don't expose the audio format. If you watch in VLC, you get a 100%-accurate
> readout; if you don't, this isn't the tool.

## Features

- 🎚️ **Exact format** — Dolby Atmos / DTS:X / TrueHD / DTS-HD MA / Dolby Digital+ / AAC / PCM,
  with channel layout, sample rate and lossless/lossy.
- 🎯 **Follows the active audio track** — shows the track VLC is actually playing (not just the
  first one) and updates when you switch tracks. *(See [limitations](#known-limitations).)*
- 🧲 **Tied to VLC** — invisible while VLC is closed; appears the moment VLC opens, hides when it
  closes. No managing it.
- 📌 **Glanceable & out of the way** — thin inline strip, pin to any corner/edge or drag anywhere.
- 📦 **Self-contained** — the .NET runtime and MediaInfo are bundled; nothing else to install.

## Install

Grab the latest [release](https://github.com/MartinMRM34/mixscope/releases/latest):

- **`mixscope-Setup-*.exe`** — installer (per-user, no admin; Start Menu entry, optional auto-start, uninstaller).
- **`mixscope-*-win-x64.zip`** — portable. Unzip and run `mixscope.exe`; right-click the bar → **Start with Windows** to auto-launch.

### One-time VLC setup
Enable VLC's Web (Lua HTTP) interface by running the included **`setup-vlc.ps1`** (closes VLC,
flips a few reversible settings, done). Then open VLC and play something — the bar appears within
~1 second.

## How it works

The audio format (Atmos / 5.1 / stereo) is metadata that only exists at the **decoder**. Windows'
audio mixer sits *after* decoding, so the OS can't distinguish them. mixscope instead asks the
player doing the decoding:

1. Query **VLC's web interface** for the file it's playing and which audio track is live.
2. Probe that file with **MediaInfo** for the exact stream flags (`Dolby Atmos`, `JOC`, `DTS:X`, …).

Browsers and Store apps expose nothing, which is why they can't be supported.

## Controls

- **Right-click → Position** — pin to any corner/edge.
- **Right-click → Start with Windows** — toggle auto-launch.
- **Drag** — place it freely (becomes a `Custom` position, remembered).
- **Hover** — see the filename as a tooltip.
- **Right-click → Exit** — close it.

## Format colours

| Badge | Meaning |
|-------|---------|
| **DOLBY ATMOS** (gold) | Atmos detected (JOC in E-AC-3, or TrueHD + Atmos) |
| **DTS:X** (blue) | DTS:X objects |
| green | lossless surround (TrueHD, DTS-HD MA, PCM multichannel) |
| teal | lossy surround (DD+, DD, DTS core) |
| grey | stereo / mono |

## Configuration

Settings live in `%APPDATA%\mixscope\config.json`:

| Key | Meaning | Default |
|-----|---------|---------|
| `VlcHost` / `VlcPort` | VLC Web interface address | `127.0.0.1` / `8080` |
| `VlcPassword` | Password set in VLC's Web interface | `""` |
| `PollMs` | How often to poll VLC (ms) | `1000` |
| `MediaInfoPath` | Explicit MediaInfo.exe (auto-detected if null) | `null` |
| `Anchor` | `TopLeft` / `TopCenter` / `TopRight` / `BottomLeft` / `BottomCenter` / `BottomRight` / `Custom` | `TopRight` |
| `Left` / `Top` | Saved position, used only when `Anchor` is `Custom` | `null` |

The password and port **must match** what `setup-vlc.ps1` configured in VLC. A rolling status
line is written to `%APPDATA%\mixscope\status.txt` for troubleshooting.

## Known limitations

- **Switching *back* to a track you already played** in the same file may keep showing the
  previous one until you pick a different track. VLC's web interface stops reporting which track is
  "live" once both have been decoded; the only interface that does (RC) opens a remote-control
  socket that antivirus flags, so it's deliberately not used. Switching to a *new* track works.
- **VLC only** — browser and Microsoft Store streaming apps can't be read (see the note up top).

## Build from source

```powershell
# Requires the .NET 8 SDK
dotnet build mixscope.csproj -c Release
./build-release.ps1 -Version 0.2.0   # self-contained exe + zip in dist/
# optional installer (needs Inno Setup): ISCC installer.iss
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT — see [LICENSE](LICENSE). Bundles the MediaInfo CLI (BSD-2-Clause) by MediaArea.
