using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mixscope.Probe;
using Mixscope.Vlc;

namespace Mixscope;

public enum OverlayAnchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Custom }

public partial class MainWindow : Window
{
    private readonly Config _cfg = Config.Load();
    private VlcClient _vlc = null!;
    private readonly DispatcherTimer _timer = new();

    private bool _polling;
    private OverlayAnchor _anchor = OverlayAnchor.TopRight;
    private string? _probedPath;
    private ProbeResult? _probe;

    // Active-audio-track tracking (reset per file).
    private readonly HashSet<string> _decodedAudioKeys = new();
    private string? _activeAudioKey;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += (_, _) => { if (_anchor != OverlayAnchor.Custom) ApplyAnchor(); };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vlc = new VlcClient(_cfg.VlcHost, _cfg.VlcPort, _cfg.VlcPassword);
        MediaProbe.MediaInfoPath = _cfg.MediaInfoPath;

        RestorePosition();
        Hide(); // start invisible; the poll loop reveals the bar only while VLC is running

        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(300, _cfg.PollMs));
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

        await PollAsync();
    }

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            // Tie the overlay's visibility to VLC: show when VLC is open, hide when it's gone.
            bool running = IsVlcRunning();
            if (running && !IsVisible) ShowOverlay();
            else if (!running && IsVisible) Hide();

            if (!running)
            {
                Diag("vlc not running -> overlay hidden");
                return;
            }

            var status = await _vlc.GetStatusAsync();
            Diag($"state={status.State} err={status.DebugError ?? "-"} file={status.FilePath ?? "-"}");
            await ApplyStatusAsync(status);
        }
        catch { /* never let the timer die */ }
        finally { _polling = false; }
    }

    private static bool IsVlcRunning()
    {
        var procs = Process.GetProcessesByName("vlc");
        try { return procs.Length > 0; }
        finally { foreach (var p in procs) p.Dispose(); }
    }

    private void ShowOverlay()
    {
        Show();
        if (_anchor == OverlayAnchor.Custom)
        {
            if (_cfg.Left is double l && _cfg.Top is double t) { Left = l; Top = t; }
        }
        else ApplyAnchor();
    }

    private async Task ApplyStatusAsync(VlcStatus st)
    {
        switch (st.State)
        {
            case PlayState.Disconnected:
                Render("#5B6273", $"VLC Web interface not reachable on {_cfg.VlcHost}:{_cfg.VlcPort}",
                    ("VLC offline", "#9298A8", false));
                return;

            case PlayState.Stopped:
                Render("#5B6273", null, ("VLC idle", "#9298A8", false));
                return;
        }

        bool paused = st.State == PlayState.Paused;

        if (st.FilePath is null)
        {
            // Live stream / non-file MRL: can't probe, show VLC's own codec hint.
            Render(paused ? "#C8A24B" : "#46E39A", st.FileName ?? st.Mrl,
                ((st.AudioCodec ?? "STREAM").ToUpperInvariant(), "#E6EAF2", true),
                (paused ? "paused" : "stream", "#9298A8", false));
            return;
        }

        if (!string.Equals(st.FilePath, _probedPath, StringComparison.OrdinalIgnoreCase))
        {
            _probedPath = st.FilePath;
            _probe = null;
            _decodedAudioKeys.Clear();
            _activeAudioKey = null;
            Render("#4BA6E8", st.FileName, ("Analyzing…", "#9298A8", false));
            _probe = await MediaProbe.ProbeAsync(st.FilePath);
        }

        UpdateActiveAudio(st.AudioStreams);
        RenderProbe(_probe!, st, paused);
    }

    private void RenderProbe(ProbeResult probe, VlcStatus st, bool paused)
    {
        string? tip = st.FileName ?? (st.FilePath is null ? null : System.IO.Path.GetFileName(st.FilePath));

        if (probe.Error is not null || probe.AudioTracks.Count == 0)
        {
            string msg = probe.Error switch
            {
                "MediaInfo.exe not found" => "MediaInfo not installed",
                null => "no audio track",
                _ => probe.Error,
            };
            Render("#C0563B", tip, (msg, "#D88A78", false));
            return;
        }

        int idx = SelectActiveTrack(probe.AudioTracks, st);
        var track = probe.AudioTracks[idx];
        Diag($"active T{idx + 1}/{probe.AudioTracks.Count} {track.PrimaryLabel} {track.ChannelText} key={_activeAudioKey}");

        string accent = AccentHex(track.Kind);
        var segs = new List<(string, string, bool)>
        {
            (track.PrimaryLabel, accent, true),
        };
        if (track.IsSpatial && !string.IsNullOrWhiteSpace(track.CodecLabel))
            segs.Add((track.CodecLabel, "#C9CFDB", false));
        if (!string.IsNullOrWhiteSpace(track.ChannelText))
            segs.Add((track.ChannelText, "#C9CFDB", false));
        if (!string.IsNullOrWhiteSpace(track.RateText))
            segs.Add((track.RateText, "#9298A8", false));
        if (!string.IsNullOrWhiteSpace(track.QualityText))
            segs.Add((track.QualityText, track.QualityText == "lossless" ? "#79C9A0" : "#9298A8", false));
        if (probe.AudioTracks.Count > 1)
        {
            string lang = string.IsNullOrWhiteSpace(track.Language) ? "" : " " + track.Language;
            segs.Add(($"T{idx + 1}/{probe.AudioTracks.Count}{lang}", "#9298A8", false));
        }

        Render(paused ? "#C8A24B" : accent, tip, segs.ToArray());
    }

    // ---- active audio track detection (HTTP-only) ----

    // The active track is the audio stream VLC most-recently started decoding. On a fresh file
    // only the default track carries Decoded_*; switching to another track makes it gain Decoded_*,
    // which we latch onto. VLC keeps Decoded_* on a track once played, so toggling back to an
    // already-played track can't be detected — a documented limitation.
    private void UpdateActiveAudio(List<VlcAudio> streams)
    {
        var decoded = streams.Where(s => s.IsDecoded).Select(s => s.Key).ToList();
        var newly = decoded.Where(k => !_decodedAudioKeys.Contains(k)).ToList();

        if (newly.Count > 0)
            _activeAudioKey = newly[^1];
        else if (_activeAudioKey is null || !decoded.Contains(_activeAudioKey))
            _activeAudioKey = decoded.FirstOrDefault() ?? streams.FirstOrDefault()?.Key;

        _decodedAudioKeys.Clear();
        foreach (var k in decoded) _decodedAudioKeys.Add(k);
    }

    // Map the active VLC audio stream onto the matching MediaInfo track (which carries the
    // authoritative Atmos/DTS:X classification).
    private int SelectActiveTrack(List<AudioTrackInfo> tracks, VlcStatus st)
    {
        if (tracks.Count <= 1 || st.AudioStreams.Count == 0) return 0;

        var active = st.AudioStreams.FirstOrDefault(s => s.Key == _activeAudioKey)
                     ?? st.AudioStreams.FirstOrDefault(s => s.IsDecoded)
                     ?? st.AudioStreams[0];

        // 1) strongest: MediaInfo track Title == VLC stream Description
        if (!string.IsNullOrWhiteSpace(active.Description))
        {
            int m = tracks.FindIndex(t => TitleMatch(t.Title, active.Description));
            if (m >= 0) return m;
        }

        // 2) codec family + language (+ channels), relaxing progressively
        string fam = CodecFamily(active.Codec);
        int ch = ChannelsFromVlc(active.DecodedChannels);

        int i = tracks.FindIndex(t => CodecFamily(t.Format) == fam && LangMatch(t.Language, active.Language) && (ch == 0 || t.Channels == ch));
        if (i >= 0) return i;
        i = tracks.FindIndex(t => CodecFamily(t.Format) == fam && LangMatch(t.Language, active.Language));
        if (i >= 0) return i;
        i = tracks.FindIndex(t => CodecFamily(t.Format) == fam && ch > 0 && t.Channels == ch);
        if (i >= 0) return i;
        i = tracks.FindIndex(t => CodecFamily(t.Format) == fam);
        return i >= 0 ? i : 0;
    }

    private static bool TitleMatch(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        a = a.Trim(); b = b.Trim();
        return a.Equals(b, StringComparison.OrdinalIgnoreCase)
               || a.Contains(b, StringComparison.OrdinalIgnoreCase)
               || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string CodecFamily(string codec)
    {
        var c = new string((codec ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (c.Contains("eac3")) return "eac3";
        if (c.Contains("truehd") || c.Contains("mlp")) return "truehd";
        if (c.Contains("dts")) return "dts";
        if (c.Contains("ac3")) return "ac3";
        if (c.Contains("aac") || c.Contains("mp4a")) return "aac";
        if (c.Contains("flac")) return "flac";
        if (c.Contains("opus")) return "opus";
        if (c.Contains("vorbis")) return "vorbis";
        if (c.Contains("pcm")) return "pcm";
        if (c.Contains("mpeg")) return "mp3";
        return c;
    }

    private static int ChannelsFromVlc(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (s.Equals("Mono", StringComparison.OrdinalIgnoreCase)) return 1;
        if (s.Equals("Stereo", StringComparison.OrdinalIgnoreCase)) return 2;
        int sum = 0;
        foreach (var ch in s) if (char.IsDigit(ch)) sum += ch - '0';
        if (s.IndexOf("LFE", StringComparison.OrdinalIgnoreCase) >= 0) sum += 1;
        return sum;
    }

    // ---- inline bar rendering ----

    private void Render(string dotHex, string? tooltip, params (string text, string hex, bool bold)[] segments)
    {
        BarPanel.Children.Clear();

        BarPanel.Children.Add(new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 0, 8, 0),
            Fill = Brush(dotHex),
            VerticalAlignment = VerticalAlignment.Center,
        });

        bool first = true;
        foreach (var (text, hex, bold) in segments)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!first) AddDivider();
            first = false;
            BarPanel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Brush(hex),
                FontSize = 12.5,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        RootBorder.ToolTip = string.IsNullOrWhiteSpace(tooltip) ? null : tooltip;
    }

    private void AddDivider()
    {
        BarPanel.Children.Add(new Border
        {
            Width = 1,
            Height = 12,
            Background = Brush("#333B49"),
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
    }

    private static string AccentHex(SpatialKind k) => k switch
    {
        SpatialKind.Atmos => "#E8B84B",
        SpatialKind.DtsX => "#5FB0F0",
        SpatialKind.LosslessSurround => "#46E39A",
        SpatialKind.LossySurround => "#84C0D8",
        SpatialKind.Stereo => "#C9CFDB",
        SpatialKind.Mono => "#C9CFDB",
        _ => "#9298A8",
    };

    private static bool LangMatch(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();
        return a.Contains(b) || b.Contains(a) || a[..Math.Min(2, a.Length)] == b[..Math.Min(2, b.Length)];
    }

    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);

    private static readonly string DiagPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mixscope", "status.txt");

    private static string _lastDiag = "";

    private static void Diag(string msg)
    {
        if (msg == _lastDiag) return;   // only log on change, keeps the file tiny
        _lastDiag = msg;
        try { System.IO.File.AppendAllText(DiagPath, $"{DateTime.Now:HH:mm:ss} {msg}\n"); }
        catch { }
    }

    // ---- window chrome ----

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        _anchor = OverlayAnchor.Custom; // user takes manual control of position
        try { DragMove(); } catch { /* ignore */ }
        _cfg.Anchor = "Custom";
        _cfg.Left = Left;
        _cfg.Top = Top;
        _cfg.Save();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Pos_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag && Enum.TryParse<OverlayAnchor>(tag, out var a))
        {
            _anchor = a;
            _cfg.Anchor = a.ToString();
            _cfg.Save();
            ApplyAnchor();
        }
    }

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
        foreach (var item in PosMenu.Items)
            if (item is MenuItem mi && mi.Tag is string tag)
                mi.IsChecked = string.Equals(tag, _anchor.ToString(), StringComparison.Ordinal);

        AutoStartItem.IsChecked = IsAutoStartEnabled();
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "mixscope";

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunValueName) != null;
        }
        catch { return false; }
    }

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (IsAutoStartEnabled())
                k.DeleteValue(RunValueName, throwOnMissingValue: false);
            else if (Environment.ProcessPath is { } exe)
                k.SetValue(RunValueName, $"\"{exe}\"");
        }
        catch { }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Config.ConfigFileForDisplay)!;
            System.IO.Directory.CreateDirectory(dir);
            _cfg.Save();
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void ApplyAnchor()
    {
        if (_anchor == OverlayAnchor.Custom) return;

        const double margin = 8;
        var wa = SystemParameters.WorkArea;
        double w = ActualWidth, h = ActualHeight;

        Left = _anchor switch
        {
            OverlayAnchor.TopLeft or OverlayAnchor.BottomLeft => wa.Left + margin,
            OverlayAnchor.TopRight or OverlayAnchor.BottomRight => wa.Right - w - margin,
            _ => wa.Left + (wa.Width - w) / 2, // top/bottom-center
        };
        Top = _anchor switch
        {
            OverlayAnchor.BottomLeft or OverlayAnchor.BottomCenter or OverlayAnchor.BottomRight => wa.Bottom - h - margin,
            _ => wa.Top + margin,
        };
    }

    private void RestorePosition()
    {
        if (!Enum.TryParse(_cfg.Anchor, out _anchor))
            _anchor = OverlayAnchor.TopRight;

        if (_anchor == OverlayAnchor.Custom)
        {
            if (_cfg.Left is double l && _cfg.Top is double t) { Left = l; Top = t; }
            else _anchor = OverlayAnchor.TopRight; // custom requested but no coords saved
        }
        // Non-custom anchors are positioned by ApplyAnchor() once the bar has a measured size.
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _cfg.Anchor = _anchor.ToString();
        if (_anchor == OverlayAnchor.Custom) { _cfg.Left = Left; _cfg.Top = Top; }
        _cfg.Save();
    }
}
