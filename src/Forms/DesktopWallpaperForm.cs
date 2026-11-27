using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ObliqueSeasons.Native;
using ObliqueSeasons.Services;

namespace ObliqueSeasons.Forms;

/// <summary>
/// Borderless WebView2 host window embedded behind desktop icons via DesktopEmbedder.
/// </summary>
internal sealed class DesktopWallpaperForm : Form
{
    private readonly WebView2 _webView;
    private CoreWebView2? _wiredCore;
    private bool _navigationCompleted;
    private bool _pendingSuspended;
    private bool _detached;
    private int _recoveryAttempts;

    public bool IsRendererReady => !IsDisposed && _navigationCompleted && _webView.CoreWebView2 is not null;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= (int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
            return cp;
        }
    }

    public DesktopWallpaperForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(0x15, 0x14, 0x16);
        Text = "ObliqueSeasonsDesktopHost";

        Rectangle virtualBounds = SystemInformation.VirtualScreen;
        Bounds = virtualBounds;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = BackColor
        };
        Controls.Add(_webView);

        Load += OnFormLoad;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            m.Result = new IntPtr(NativeMethods.MA_NOACTIVATE);
            return;
        }
        base.WndProc(ref m);
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        ReEmbed();
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (IsDisposed || _detached) return;

        try
        {
            await WebViewEnvironmentHost.ConfigureWebViewAsync(
                _webView,
                WebViewEnvironmentHost.WallpaperDirectory);
            if (IsDisposed || _detached) return;

            CoreWebView2 core = _webView.CoreWebView2;
            if (!ReferenceEquals(_wiredCore, core))
            {
                _wiredCore = core;
                core.NavigationCompleted += OnNavigationCompleted;
                core.ProcessFailed += OnCoreProcessFailed;
            }
            core.Navigate($"{WebViewEnvironmentHost.BaseUrl}?desktop=1&host=1");
        }
        catch
        {
            // Initialization failures are handled gracefully without crashing the shell host.
        }
    }

    private async void OnCoreProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _navigationCompleted = false;
        if (IsDisposed || _detached || _recoveryAttempts >= 3) return;
        _recoveryAttempts++;
        try
        {
            await Task.Delay(2000);
            await InitializeWebViewAsync();
        }
        catch
        {
            // Best-effort recovery.
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || IsDisposed) return;

        try
        {
            bool ready = await WebViewEnvironmentHost.WaitForHostApiReadyAsync(_webView, 8000);
            if (IsDisposed) return;

            _navigationCompleted = ready;
            if (ready)
            {
                _recoveryAttempts = 0;
                if (_pendingSuspended)
                {
                    await WebViewEnvironmentHost.SetHostSuspendedAsync(_webView, true);
                }
            }
        }
        catch
        {
            // Ignore transient navigation completion errors during teardown.
        }
    }

    public void ReEmbed()
    {
        if (IsDisposed || !IsHandleCreated) return;
        DesktopEmbedder.Embed(Handle, SystemInformation.VirtualScreen);
    }

    public async Task SetHostSuspendedAsync(bool suspended)
    {
        _pendingSuspended = suspended;
        if (!IsRendererReady) return;
        await WebViewEnvironmentHost.SetHostSuspendedAsync(_webView, suspended);
    }

    public Task<(byte[] Bytes, string Extension)> CaptureSnapshotAsync(int width, int height)
    {
        return WebViewEnvironmentHost.CaptureSnapshotFromWebViewAsync(_webView, width, height);
    }

    public void DetachNow()
    {
        if (_detached) return;
        _detached = true;
        if (IsHandleCreated)
        {
            DesktopEmbedder.DetachAndRefresh(Handle);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        DetachNow();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachNow();
            _webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
