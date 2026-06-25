using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Mixscope.Probe;

public enum SpatialKind { Atmos, DtsX, LosslessSurround, LossySurround, Stereo, Mono, Other }

public sealed class AudioTrackInfo
{
    public string Format = "";             // raw MediaInfo Format, e.g. "E-AC-3", "MLP FBA", "DTS", "AAC", "PCM"
    public string Commercial = "";         // Format_Commercial_IfAny, e.g. "Dolby Digital Plus with Dolby Atmos"
    public string AdditionalFeatures = ""; // e.g. "JOC", "XLL X"
    public int Channels;
    public string ChannelLayout = "";      // e.g. "L R C LFE Ls Rs"
    public string Language = "";
    public string Title = "";
    public int SamplingRate;               // Hz
    public long BitRate;                   // bps

    public bool IsAtmos;
    public bool IsDtsX;
    public bool IsSpatial;
    public SpatialKind Kind = SpatialKind.Other;

    // Discrete fields for the inline bar:
    public string PrimaryLabel = "";   // "DOLBY ATMOS" / "DTS:X" / "Dolby Digital+" / "PCM" ...
    public string CodecLabel = "";     // underlying codec for spatial formats ("TrueHD", "DTS")
    public string ChannelText = "";    // "7.1"
    public string RateText = "";       // "48 kHz"
    public string QualityText = "";    // "lossless" / "lossy"
}

public sealed class ProbeResult
{
    public string FilePath = "";
    public List<AudioTrackInfo> AudioTracks = new();
    public string? Error;
}

/// <summary>Runs MediaInfo on a file and turns its JSON into structured audio-format info.</summary>
public static class MediaProbe
{
    /// <summary>Explicit MediaInfo.exe path from config; auto-detected when null/empty.</summary>
    public static string? MediaInfoPath { get; set; }

    public static string? ResolveMediaInfo()
    {
        if (!string.IsNullOrWhiteSpace(MediaInfoPath) && File.Exists(MediaInfoPath))
            return MediaInfoPath;

        // Prefer a MediaInfo.exe shipped next to our own exe (self-sufficient release package).
        var bundled = System.IO.Path.Combine(AppContext.BaseDirectory, "MediaInfo.exe");
        if (File.Exists(bundled)) return bundled;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var candidate = System.IO.Path.Combine(dir.Trim(), "MediaInfo.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* bad PATH entry */ }
        }

        // winget installs a shim here:
        var link = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Links", "MediaInfo.exe");
        return File.Exists(link) ? link : null;
    }

    public static async Task<ProbeResult> ProbeAsync(string filePath)
    {
        var result = new ProbeResult { FilePath = filePath };

        var mi = ResolveMediaInfo();
        if (mi is null) { result.Error = "MediaInfo.exe not found"; return result; }
        if (!File.Exists(filePath)) { result.Error = "File not found"; return result; }

        string json;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = mi,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("--Output=JSON");
            psi.ArgumentList.Add(filePath);

            using var proc = Process.Start(psi)!;
            // Hard timeout: a probe must never hang the overlay's poll loop (e.g. MediaInfo
            // stalling on a file on a slow/sleeping/disconnected drive).
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                json = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                result.Error = "MediaInfo timed out";
                return result;
            }
        }
        catch (Exception ex) { result.Error = ex.Message; return result; }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("media", out var media) ||
                !media.TryGetProperty("track", out var tracks) ||
                tracks.ValueKind != JsonValueKind.Array)
            {
                result.Error = "No tracks reported";
                return result;
            }

            foreach (var tr in tracks.EnumerateArray())
            {
                if (!string.Equals(Str(tr, "@type"), "Audio", StringComparison.OrdinalIgnoreCase)) continue;

                var a = new AudioTrackInfo
                {
                    Format = Str(tr, "Format"),
                    Commercial = Str(tr, "Format_Commercial_IfAny"),
                    AdditionalFeatures = Str(tr, "Format_AdditionalFeatures"),
                    ChannelLayout = Str(tr, "ChannelLayout"),
                    Language = Str(tr, "Language"),
                    Title = Str(tr, "Title"),
                    Channels = IntOf(tr, "Channels"),
                    SamplingRate = IntOf(tr, "SamplingRate"),
                    BitRate = LongOf(tr, "BitRate"),
                };
                Classify(a);
                result.AudioTracks.Add(a);
            }

            if (result.AudioTracks.Count == 0) result.Error = "No audio tracks";
        }
        catch (Exception ex) { result.Error = "parse: " + ex.Message; }

        return result;
    }

    private static void Classify(AudioTrackInfo a)
    {
        string comm = a.Commercial.ToLowerInvariant();
        string feat = a.AdditionalFeatures.ToLowerInvariant();
        string fmt = a.Format.ToUpperInvariant();

        a.IsAtmos = comm.Contains("atmos") || feat.Contains("joc");
        a.IsDtsX = comm.Contains("dts:x") || comm.Contains("dts-x") || feat.Contains("xll x");

        a.ChannelText = ChannelLabel(a.Channels, a.ChannelLayout);
        a.RateText = a.SamplingRate > 0 ? $"{a.SamplingRate / 1000.0:0.#} kHz" : "";

        bool lossless = fmt.Contains("MLP") || fmt == "PCM" || fmt.Contains("FLAC") || fmt.Contains("ALAC")
                        || comm.Contains("master audio") || comm.Contains("truehd");
        a.QualityText = lossless ? "lossless" : "lossy";

        if (a.IsAtmos)
        {
            a.IsSpatial = true;
            a.Kind = SpatialKind.Atmos;
            a.PrimaryLabel = "DOLBY ATMOS";
            a.CodecLabel = fmt.Contains("MLP") ? "TrueHD"
                         : (fmt.Contains("E-AC-3") || fmt.Contains("AC-3")) ? "Dolby Digital+"
                         : CodecName(fmt, comm);
        }
        else if (a.IsDtsX)
        {
            a.IsSpatial = true;
            a.Kind = SpatialKind.DtsX;
            a.PrimaryLabel = "DTS:X";
            a.CodecLabel = "DTS";
        }
        else
        {
            a.PrimaryLabel = CodecName(fmt, comm);
            a.Kind = a.Channels <= 1 ? SpatialKind.Mono
                   : a.Channels == 2 ? SpatialKind.Stereo
                   : lossless ? SpatialKind.LosslessSurround
                   : SpatialKind.LossySurround;
        }
    }

    // ---- helpers ----

    private static string ChannelLabel(int channels, string layout)
    {
        if (channels <= 0) return "?";
        bool lfe = layout.ToUpperInvariant().Contains("LFE");
        return lfe ? $"{channels - 1}.1" : $"{channels}.0";
    }

    private static string CodecName(string fmt, string comm)
    {
        if (fmt.Contains("MLP")) return "Dolby TrueHD";
        if (fmt.Contains("E-AC-3")) return "Dolby Digital+";
        if (fmt.Contains("AC-3")) return "Dolby Digital";
        if (fmt.Contains("DTS"))
        {
            if (comm.Contains("master audio")) return "DTS-HD MA";
            if (comm.Contains("high resolution")) return "DTS-HD HR";
            return "DTS";
        }
        if (fmt.Contains("AAC")) return "AAC";
        if (fmt == "PCM") return "PCM";
        if (fmt.Contains("FLAC")) return "FLAC";
        if (fmt.Contains("OPUS")) return "Opus";
        if (fmt.Contains("VORBIS")) return "Vorbis";
        if (fmt.Contains("MPEG AUDIO")) return "MP3";
        return string.IsNullOrEmpty(fmt) ? "Audio" : fmt;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int IntOf(JsonElement e, string name) =>
        int.TryParse(Str(e, name), out var n) ? n : 0;

    private static long LongOf(JsonElement e, string name) =>
        long.TryParse(Str(e, name), out var n) ? n : 0;
}
