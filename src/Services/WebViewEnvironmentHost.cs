using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ObliqueSeasons.Models;

namespace ObliqueSeasons.Services;

/// <summary>
/// Manages the shared CoreWebView2Environment, virtual host mapping, and snapshot IPC helpers
/// so all WebView2 views share https://oblique.local/, localStorage, and BroadcastChannel.
/// </summary>
internal static class WebViewEnvironmentHost
{
    public const string VirtualHostName = "oblique.local";
    public static readonly string BaseUrl = $"https://{VirtualHostName}/index.html";

    private static readonly SemaphoreSlim EnvLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static CoreWebView2Environment? _sharedEnv;

    private sealed class SnapshotScriptResult
    {
        public bool Ok { get; set; }
        public string? DataUrl { get; set; }
        public string? Error { get; set; }
    }

    public static string ProfileDirectory => Path.Combine(AppSettings.DataRootDirectory, "WebView2Profile");

    public static string WallpaperDirectory => Path.Combine(AppContext.BaseDirectory, "wallpaper");

    public static void PrewarmEnvironment()
    {
        _ = PrewarmCoreAsync();

        static async Task PrewarmCoreAsync()
        {
            try
            {
                await GetOrCreateEnvironmentAsync();
            }
            catch
            {
                // Startup error handling will surface detailed state when initializing WebView2.
            }
        }
    }

    public static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
    {
        if (_sharedEnv is not null)
        {
            return _sharedEnv;
        }

        await EnvLock.WaitAsync();
        try
        {
            if (_sharedEnv is null)
            {
                Directory.CreateDirectory(ProfileDirectory);
                CoreWebView2EnvironmentOptions options = new(
                    "--autoplay-policy=no-user-gesture-required");
                _sharedEnv = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: ProfileDirectory,
                    options: options);
            }
            return _sharedEnv;
        }
        finally
        {
            EnvLock.Release();
        }
    }

    public static async Task ConfigureWebViewAsync(WebView2 webView, string wallpaperDir)
    {
        CoreWebView2Environment env = await GetOrCreateEnvironmentAsync();
        await webView.EnsureCoreWebView2Async(env);

        CoreWebView2 core = webView.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            wallpaperDir,
            CoreWebView2HostResourceAccessKind.Allow);

        CoreWebView2Settings settings = core.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreDevToolsEnabled = false;
    }

    public static async Task<bool> WaitForHostApiReadyAsync(WebView2 webView, int timeoutMs = 10000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (webView.IsDisposed || webView.CoreWebView2 is null)
            {
                return false;
            }

            try
            {
                string res = await webView.CoreWebView2.ExecuteScriptAsync(
                    "typeof window.obliqueHostCaptureSnapshot === 'function' && window.obliqueRendererReady === true");
                if (string.Equals(res, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Page may still be initializing.
            }

            await Task.Delay(120);
        }
        return false;
    }

    public static async Task<(byte[] Bytes, string Extension)> CaptureSnapshotFromWebViewAsync(
        WebView2 webView, int width, int height)
    {
        if (webView.IsDisposed || webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("渲染器尚未初始化。");
        }

        string script =
            $"window.obliqueHostCaptureSnapshot ? window.obliqueHostCaptureSnapshot({width}, {height}) : {{ ok: false, error: 'renderer_not_ready' }}";
        string rawJson = await webView.CoreWebView2.ExecuteScriptAsync(script);

        SnapshotScriptResult? result = JsonSerializer.Deserialize<SnapshotScriptResult>(rawJson, JsonOptions);
        if (result is null || !result.Ok || string.IsNullOrWhiteSpace(result.DataUrl))
        {
            throw new InvalidOperationException($"渲染快照失败：{result?.Error ?? "unknown_error"}");
        }

        return DecodeDataUrl(result.DataUrl);
    }

    public static async Task SetHostSuspendedAsync(WebView2 webView, bool suspended)
    {
        if (webView.IsDisposed || webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            string arg = suspended ? "true" : "false";
            await webView.CoreWebView2.ExecuteScriptAsync(
                $"if (window.obliqueHostSetPaused) window.obliqueHostSetPaused({arg});");
        }
        catch
        {
            // Ignore transient script calls during teardown.
        }
    }

    /// <summary>
    /// Decodes a canvas.toDataURL() result into raw image bytes and file extension.
    /// </summary>
    public static (byte[] Bytes, string Extension) DecodeDataUrl(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            throw new InvalidOperationException("截图返回为空。");
        }

        int commaIdx = dataUrl.IndexOf(',');
        if (commaIdx < 0)
        {
            throw new InvalidOperationException("无效的截图数据格式。");
        }

        string header = dataUrl[..commaIdx];
        string base64 = dataUrl[(commaIdx + 1)..];
        string ext = header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        byte[] bytes = Convert.FromBase64String(base64);
        return (bytes, ext);
    }
}
