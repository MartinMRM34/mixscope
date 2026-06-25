# Changelog

All notable changes to **mixscope** are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [0.2.1] — 2026-06-25

### Fixed
- The overlay could **freeze** (stay stuck on a stale state and read "VLC offline") if a MediaInfo
  probe hung on a file — e.g. one on a slow, sleeping, or disconnected drive. Probes now have a
  hard 12-second timeout, so a stuck probe can never block the poll loop. If a probe does time out,
  the bar shows a brief error and recovers on the next poll.

## [0.2.0] — 2026-06-25

### Added
- **Active audio-track detection.** The bar now shows the audio track VLC is *actually* playing
  (not just the first track) and updates when you switch to a track you haven't played yet. It
  reads each audio stream's decoded state from VLC's web interface and maps the live one to the
  matching MediaInfo track by title / codec / language / channels.
- **App icon** — an equalizer mark in the format-accent colours, on the executable, the installer,
  and the Start Menu shortcut.

### Known limitations
- Switching *back* to a track you already played in the same file may keep showing the previous
  one until you pick a different track — VLC's web interface stops reporting which track is "live"
  once both have been decoded, and the one interface that does (RC) is avoided because antivirus
  flags its remote-control socket.

## [0.1.0] — 2026-06-25

### Added
- Initial release. Always-on-top inline overlay showing the real audio format of whatever VLC is
  playing: Dolby Atmos, DTS:X, TrueHD, DTS-HD MA, Dolby Digital+, AAC, PCM — with channel layout
  (2.0 / 5.1 / 7.1), sample rate and lossless/lossy.
- Reads VLC's web interface for the current file and probes it with MediaInfo for exact stream
  flags — 100% accurate, not guessed from the OS mixer.
- Visibility tied to VLC's lifecycle (shows on open, hides on close); positionable to any
  corner/edge or draggable; in-app "Start with Windows" toggle.
- Self-contained Windows installer and portable zip — the .NET runtime and MediaInfo are bundled.

[0.2.1]: https://github.com/MartinMRM34/mixscope/releases/tag/v0.2.1
[0.2.0]: https://github.com/MartinMRM34/mixscope/releases/tag/v0.2.0
[0.1.0]: https://github.com/MartinMRM34/mixscope/releases/tag/v0.1.0
