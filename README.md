# mixscope

A small always-on-top Windows overlay that shows the **real audio format of whatever
[VLC](https://www.videolan.org/) is playing** — Dolby Atmos, DTS:X, TrueHD, DTS-HD MA,
Dolby Digital+, AAC, PCM — with channel layout (2.0 / 5.1 / 7.1), sample rate, and
lossless/lossy, in a thin NVIDIA-style strip.

> ⚠️ **Works with VLC only.** mixscope reads VLC's web interface to know what's playing.
> It does **not** work with browsers (Netflix/Prime in a tab), the Microsoft Store streaming
> apps, or other media players — those don't expose the audio format to the system. If you
> watch in VLC, you get a 100%-accurate readout; if you don't, this isn't the tool.

It mirrors the kind of "now playing format" readout a Sony Bravia / AV receiver shows —
but for your PC, and only for VLC.

## Why VLC only

The audio format (Atmos / 5.1 / stereo) is metadata that only exists at the **decoder**.
Windows' audio mixer sits *after* decoding, so the OS can't tell Atmos from 5.1 from stereo.
The reliable way to know is to ask the player that's decoding it. **VLC** exposes the currently
playing file over a local web interface — mixscope reads that, then probes the file with
**MediaInfo** for the exact stream flags (`Dolby Atmos`, `JOC`, `DTS:X`, …). Browsers and Store
apps expose nothing, so they can't be supported.

## Install

Grab the latest [release](../../releases):

- **`mixscope-Setup-*.exe`** — installer (per-user, no admin; Start Menu entry, optional auto-start, uninstaller).
- **`mixscope-*-win-x64.zip`** — portable. Unzip and run `mixscope.exe`; right-click the bar → **Start with Windows** to auto-launch.

Both are **self-contained** (the .NET runtime and MediaInfo are bundled — nothing else to install).

### One-time VLC setup
Enable VLC's Web (Lua HTTP) interface by running the included **`setup-vlc.ps1`**
(closes VLC, flips a few reversible settings, done). Then open VLC and play something —
the bar appears within ~1 second.

## Build from source

```powershell
# Requires the .NET 8 SDK
dotnet build mixscope.csproj -c Release
./build-release.ps1            # produces the self-contained exe + zip in dist/
```

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

The password and port **must match** what `setup-vlc.ps1` configured in VLC.

## Controls

- **Right-click → Position** to pin it to any corner/edge.
- **Right-click → Start with Windows** to toggle auto-launch.
- **Drag** anywhere to place it freely (becomes a `Custom` position, remembered).
- **Hover** to see the filename as a tooltip.
- **Right-click → Exit** to close.

## Tied to VLC

mixscope is a background companion for VLC: it stays invisible while VLC is closed and
**appears when VLC opens, hides when VLC closes**. A rolling status line is written to
`%APPDATA%\mixscope\status.txt` for troubleshooting.

## Format colours

| Badge | Meaning |
|-------|---------|
| **DOLBY ATMOS** (gold) | Atmos detected (JOC in E-AC-3, or TrueHD + Atmos) |
| **DTS:X** (blue) | DTS:X objects |
| green | lossless surround (TrueHD, DTS-HD MA, PCM multichannel) |
| teal | lossy surround (DD+, DD, DTS core) |
| grey | stereo / mono |

## Known limitations

- **Audio-track switching:** mixscope shows the correct *default* track and follows a switch to a
  track you haven't played yet in the current file. But if you switch **back** to a track you
  already played earlier in the same sitting, the bar may keep showing the previous one until you
  pick a different track. VLC's web interface stops reporting which track is "live" once both have
  been decoded, and the one interface that does (RC) opens a remote-control socket that antivirus
  flags — so it's deliberately not used.
- **VLC only** — see the note at the top; browser and Microsoft Store streaming apps can't be read.

## License

MIT — see [LICENSE](LICENSE). Bundles the MediaInfo CLI (BSD-2-Clause) by MediaArea.
