using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ObliqueSeasons.Native;
using ObliqueSeasons.Services;

namespace ObliqueSeasons.Forms;

/// <summary>
/// Ephemeral off-screen WebView2 host used for background lock-screen/desktop snapshot capture
/// when neither DesktopWallpaperForm nor MainControlForm is currently active.
/// </summary>
internal sealed class OffscreenCaptureForm : Form
{
    private readonly WebView2 _webView;
    private readonly TaskCompletionSource<bool> _navTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    private OffscreenCaptureForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(640, 360);
        Opacity = 0;
        BackColor = Color.FromArgb(0x15, 0x14, 0x16);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = BackColor
        };
        Controls.Add(_webView);
    }

    public static async Task<(byte[] Bytes, string Extension)> CaptureOffscreenAsync(int width, int height)
    {
        using OffscreenCaptureForm form = new();
        form.Show();
        try
        {
            await WebViewEnvironmentHost.ConfigureWebViewAsync(
                form._webView,
                WebViewEnvironmentHost.WallpaperDirectory);

            form._webView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                form._navTcs.TrySetResult(args.IsSuccess);
            };
            form._webView.CoreWebView2.Navigate($"{WebViewEnvironmentHost.BaseUrl}?desktop=1&host=1");

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
            Task completed = await Task.WhenAny(form._navTcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != form._navTcs.Task || !await form._navTcs.Task)
            {
                throw new TimeoutException("离线渲染主机加载超时。");
            }

            bool ready = await WebViewEnvironmentHost.WaitForHostApiReadyAsync(form._webView, 10000);
            if (!ready)
            {
                throw new InvalidOperationException("渲染脚本接口未能就绪。");
            }

            // Allow one brief tick for canvas/shaders/weather initialization to settle before snapshotting.
            await Task.Delay(250);
            return await WebViewEnvironmentHost.CaptureSnapshotFromWebViewAsync(form._webView, width, height);
        }
        finally
        {
            form.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
