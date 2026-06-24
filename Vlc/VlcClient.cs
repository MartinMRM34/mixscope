using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AudioChannelOverlay.Vlc;

public enum PlayState { Disconnected, Stopped, Paused, Playing }

/// <summary>Snapshot of what VLC is doing right now.</summary>
public sealed class VlcStatus
{
    public PlayState State = PlayState.Disconnected;
    public string? FilePath;     // local path, if the MRL is a file://
    public string? FileName;
    public string? Mrl;          // raw MRL/uri from the playlist
    public double PositionSec;
    public double LengthSec;

    // Best-effort hints about the *active* audio track, as VLC reports it.
    // Present only when VLC is rendering audio (a real GUI instance); used to
    // disambiguate which audio track to highlight when a file has several.
    public string? AudioCodec;
    public string? AudioLanguage;

    /// <summary>Set when the status request failed, for diagnostics.</summary>
    public string? DebugError;
}

/// <summary>
/// Talks to VLC's Web (Lua HTTP) interface: /requests/status.json and /requests/playlist.json.
/// </summary>
public sealed class VlcClient
{
    // UseProxy=false: talk to VLC on localhost directly. A system/VPN proxy that doesn't
    // bypass localhost would otherwise make every request fail (shows as "VLC offline").
    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(3),
    };
    private string _host;
    private int _port;

    public VlcClient(string host, int port, string password)
    {
        _host = host;
        _port = port;
        SetAuth(password);
    }

    public void Update(string host, int port, string password)
    {
        _host = host;
        _port = port;
        SetAuth(password);
    }

    private void SetAuth(string password)
    {
        // VLC uses HTTP Basic auth with an empty username.
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + password));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private string Base => $"http://{_host}:{_port}/requests";

    public async Task<VlcStatus> GetStatusAsync()
    {
        var st = new VlcStatus();

        string statusJson;
        try
        {
            statusJson = await _http.GetStringAsync($"{Base}/status.json");
        }
        catch (Exception ex)
        {
            st.State = PlayState.Disconnected;
            st.DebugError = ex.GetType().Name + ": " + ex.GetBaseException().Message;
            return st;
        }

        try
        {
            using var doc = JsonDocument.Parse(statusJson);
            var root = doc.RootElement;

            st.State = (root.TryGetProperty("state", out var se) ? se.GetString() : null) switch
            {
                "playing" => PlayState.Playing,
                "paused" => PlayState.Paused,
                _ => PlayState.Stopped,
            };

            if (root.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Number) st.PositionSec = t.GetDouble();
            if (root.TryGetProperty("length", out var l) && l.ValueKind == JsonValueKind.Number) st.LengthSec = l.GetDouble();

            if (root.TryGetProperty("information", out var info) &&
                info.TryGetProperty("category", out var cat) &&
                cat.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in cat.EnumerateObject())
                {
                    if (prop.Name == "meta")
                    {
                        if (prop.Value.TryGetProperty("filename", out var fn)) st.FileName = fn.GetString();
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object &&
                             prop.Value.TryGetProperty("Type", out var ty) &&
                             string.Equals(ty.GetString(), "Audio", StringComparison.OrdinalIgnoreCase))
                    {
                        if (st.AudioCodec is null && prop.Value.TryGetProperty("Codec", out var cc)) st.AudioCodec = cc.GetString();
                        if (st.AudioLanguage is null && prop.Value.TryGetProperty("Language", out var lg)) st.AudioLanguage = lg.GetString();
                    }
                }
            }
        }
        catch
        {
            // Malformed status; treat as connected-but-idle.
            st.State = PlayState.Stopped;
        }

        // The current MRL (full path) lives in the playlist, flagged current:"current".
        if (st.State is PlayState.Playing or PlayState.Paused)
        {
            try
            {
                var plJson = await _http.GetStringAsync($"{Base}/playlist.json");
                using var pdoc = JsonDocument.Parse(plJson);
                var mrl = FindCurrentMrl(pdoc.RootElement);
                if (!string.IsNullOrEmpty(mrl))
                {
                    st.Mrl = mrl;
                    if (Uri.TryCreate(mrl, UriKind.Absolute, out var u) && u.IsFile)
                    {
                        st.FilePath = u.LocalPath;
                        st.FileName ??= System.IO.Path.GetFileName(st.FilePath);
                    }
                }
            }
            catch { /* keep whatever we have */ }
        }

        return st;
    }

    private static string? FindCurrentMrl(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object) return null;

        if (node.TryGetProperty("current", out _) && node.TryGetProperty("uri", out var uri))
            return uri.GetString();

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                var found = FindCurrentMrl(child);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
