using System.Drawing.Drawing2D;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ObliqueSeasons.Native;
using ObliqueSeasons.Services;

namespace ObliqueSeasons.Forms;

/// <summary>
/// Primary interactive control & preview window hosting the WebView2 UI.
/// </summary>
internal sealed class MainControlForm : Form
{
    private static readonly JsonSerializerOptions HostJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class PillRetryButton : Button
    {
        private bool _hovered;
        private bool _pressed;

        public PillRetryButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.FromArgb(0x15, 0x14, 0x16));

            Color fill = _pressed
                ? Color.FromArgb(0xD2, 0xD2, 0xDA)
                : (_hovered ? Color.FromArgb(0xEB, 0xEB, 0xF0) : Color.FromArgb(0xFF, 0xFF, 0xFF));

            RectangleF rect = new(1f, 1f, Width - 2.5f, Height - 2.5f);
            float radius = rect.Height * 0.5f;
            using GraphicsPath path = new();
            path.AddArc(rect.X, rect.Y, radius * 2, rect.Height, 90, 180);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, rect.Height, 270, 180);
            path.CloseFigure();

            using SolidBrush bgBrush = new(fill);
            g.FillPath(bgBrush, path);

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                ClientRectangle,
                Color.FromArgb(0x14, 0x14, 0x16),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    private sealed class StartupOverlayControl : Control
    {
        private readonly System.Windows.Forms.Timer _spinTimer;
        private readonly PillRetryButton _retryButton;
        private float _angle;
        private bool _isError;
        private string _message = string.Empty;

        public event EventHandler? RetryClicked;

        public StartupOverlayControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(0x15, 0x14, 0x16);

            _retryButton = new PillRetryButton
            {
                Visible = false,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                Size = new Size(104, 34)
            };
            _retryButton.Click += (s, e) => RetryClicked?.Invoke(this, e);
            Controls.Add(_retryButton);

            _spinTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _spinTimer.Tick += (_, _) =>
            {
                _angle = (_angle + 11f) % 360f;
                Invalidate();
            };
        }

        public void ShowLoading(string message)
        {
            _isError = false;
            _message = message;
            _retryButton.Visible = false;
            Visible = true;
            BringToFront();
            if (!_spinTimer.Enabled)
            {
                _spinTimer.Start();
            }
            Invalidate();
        }

        public void ShowError(string message, string retryText)
        {
            _isError = true;
            _message = message;
            _spinTimer.Stop();
            _retryButton.Text = retryText;
            LayoutRetryButton();
            _retryButton.Visible = true;
            Visible = true;
            BringToFront();
            Invalidate();
        }

        public void UpdateErrorTexts(string message, string retryText)
        {
            _message = message;
            _retryButton.Text = retryText;
            LayoutRetryButton();
            Invalidate();
        }

        public void UpdateLoadingText(string message)
        {
            _message = message;
            Invalidate();
        }

        public bool IsErrorState => _isError;

        public void HideOverlay()
        {
            _spinTimer.Stop();
            _retryButton.Visible = false;
            Visible = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutRetryButton();
        }

        private void LayoutRetryButton()
        {
            int btnW = Math.Max(98, TextRenderer.MeasureText(_retryButton.Text, _retryButton.Font).Width + 44);
            int btnH = 34;
            _retryButton.Size = new Size(btnW, btnH);
            _retryButton.Location = new Point((Width - btnW) / 2, (Height / 2) + 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int cx = Width / 2;
            int cy = Height / 2;

            if (!_isError)
            {
                const float size = 22f;
                RectangleF ring = new(cx - size * 0.5f, cy - 22f, size, size);
                using Pen trackPen = new(Color.FromArgb(42, 255, 255, 255), 2.1f);
                g.DrawEllipse(trackPen, ring);

                using Pen arcPen = new(Color.FromArgb(224, 255, 255, 255), 2.1f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawArc(arcPen, ring, _angle, 230f);

                using Font font = new("Microsoft YaHei UI", 9.75f, FontStyle.Regular);
                Rectangle textRect = new(Math.Max(24, cx - 220), cy + 12, Math.Min(440, Width - 48), 32);
                TextRenderer.DrawText(
                    g,
                    _message,
                    font,
                    textRect,
                    Color.FromArgb(0xD2, 0xD2, 0xDA),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
            else
            {
                using Font font = new("Microsoft YaHei UI", 10.25f, FontStyle.Regular);
                int maxTextW = Math.Min(440, Math.Max(200, Width - 64));
                Size measured = TextRenderer.MeasureText(
                    g,
                    _message,
                    font,
                    new Size(maxTextW, 200),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
                int textTop = (cy - 10) - measured.Height;
                Rectangle textRect = new((Width - maxTextW) / 2, Math.Max(24, textTop), maxTextW, measured.Height + 6);
                TextRenderer.DrawText(
                    g,
                    _message,
                    font,
                    textRect,
                    Color.FromArgb(0xEC, 0xEC, 0xF0),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinTimer.Stop();
                _spinTimer.Dispose();
                _retryButton.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private enum StartupFailureKind
    {
        None,
        Generic,
        RuntimeMissing,
        Timeout
    }

    private readonly WebView2 _webView;
    private readonly StartupOverlayControl _startupOverlay;
    private readonly System.Windows.Forms.Timer _startupTimeoutTimer;
    private readonly Func<bool> _shouldMinimizeToTray;
    private readonly Action _onMinimizedToTray;
    private readonly Action _onRealClose;
    private readonly Action<string, JsonElement> _onWebMessage;
    private readonly Action _onRendererReady;
    private readonly Func<Localization.HostStrings> _getStrings;

    private CoreWebView2? _wiredCore;
    private StartupFailureKind _failureKind = StartupFailureKind.None;
    private bool _processFailed;
    private bool _rendererReady;
    private bool _hostSuspended;
    private bool _allowRealClose;
    private FormWindowState _preFullscreenWindowState = FormWindowState.Normal;
    private FormBorderStyle _preFullscreenBorderStyle = FormBorderStyle.Sizable;
    private Rectangle _preFullscreenBounds;
    private bool _isFullscreen;

    public bool IsRendererReady => !IsDisposed && _rendererReady && _webView.CoreWebView2 is not null;

    public MainControlForm(
        Icon appIcon,
        Func<Localization.HostStrings> getStrings,
        Func<bool> shouldMinimizeToTray,
        Action onMinimizedToTray,
        Action onRealClose,
        Action<string, JsonElement> onWebMessage,
        Action onRendererReady)
    {
        _getStrings = getStrings;
        _shouldMinimizeToTray = shouldMinimizeToTray;
        _onMinimizedToTray = onMinimizedToTray;
        _onRealClose = onRealClose;
        _onWebMessage = onWebMessage;
        _onRendererReady = onRendererReady;

        Text = _getStrings().AppTitle;
        Icon = appIcon;
        Size = new Size(1240, 760);
        MinimumSize = new Size(860, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0x15, 0x14, 0x16);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = BackColor
        };
        _startupOverlay = new StartupOverlayControl();
        _startupOverlay.RetryClicked += async (_, _) => await RetryStartupAsync();
        Controls.Add(_webView);
        Controls.Add(_startupOverlay);
        _startupOverlay.BringToFront();
        _startupOverlay.ShowLoading(_getStrings().StartupLoading);

        _startupTimeoutTimer = new System.Windows.Forms.Timer { Interval = 22000 };
        _startupTimeoutTimer.Tick += (_, _) =>
        {
            _startupTimeoutTimer.Stop();
            if (!IsDisposed && !_rendererReady)
            {
                ShowStartupError(StartupFailureKind.Timeout);
            }
        };

        Load += OnFormLoad;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.EnableDarkTitleBar(Handle);
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (IsDisposed) return;

        _rendererReady = false;
        _processFailed = false;
        _failureKind = StartupFailureKind.None;
        _startupOverlay.ShowLoading(_getStrings().StartupLoading);
        _startupTimeoutTimer.Stop();
        _startupTimeoutTimer.Start();

        try
        {
            await WebViewEnvironmentHost.ConfigureWebViewAsync(
                _webView,
                WebViewEnvironmentHost.WallpaperDirectory);
            if (IsDisposed) return;

            CoreWebView2 core = _webView.CoreWebView2;
            if (!ReferenceEquals(_wiredCore, core))
            {
                _wiredCore = core;
                core.NavigationCompleted += OnNavigationCompleted;
                core.WebMessageReceived += OnWebMessageReceived;
                core.ContainsFullScreenElementChanged += OnContainsFullScreenElementChanged;
                core.ProcessFailed += OnCoreProcessFailed;
            }
            core.Navigate($"{WebViewEnvironmentHost.BaseUrl}?panel=1&host=1");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError(StartupFailureKind.RuntimeMissing);
        }
        catch
        {
            ShowStartupError(StartupFailureKind.Generic);
        }
    }

    private async Task RetryStartupAsync()
    {
        if (IsDisposed) return;

        if (_webView.CoreWebView2 is null || _processFailed)
        {
            await InitializeWebViewAsync();
            return;
        }

        _rendererReady = false;
        _failureKind = StartupFailureKind.None;
        _startupOverlay.ShowLoading(_getStrings().StartupLoading);
        _startupTimeoutTimer.Stop();
        _startupTimeoutTimer.Start();
        try
        {
            _webView.CoreWebView2.Navigate($"{WebViewEnvironmentHost.BaseUrl}?panel=1&host=1");
        }
        catch
        {
            await InitializeWebViewAsync();
        }
    }

    private void ShowStartupError(StartupFailureKind kind)
    {
        if (IsDisposed) return;
        _startupTimeoutTimer.Stop();
        _rendererReady = false;
        _failureKind = kind;
        Localization.HostStrings s = _getStrings();
        _startupOverlay.ShowError(GetFailureMessage(s, kind), s.RetryButton);
    }

    private static string GetFailureMessage(Localization.HostStrings s, StartupFailureKind kind) => kind switch
    {
        StartupFailureKind.RuntimeMissing => s.StartupFailedRuntime,
        StartupFailureKind.Timeout => s.StartupFailedTimeout,
        _ => s.StartupFailedGeneric
    };

    public void UpdateLocalizedTitle(string title)
    {
        if (!IsDisposed)
        {
            Text = title;
        }
    }

    public void RefreshLocalization()
    {
        if (IsDisposed) return;
        Localization.HostStrings s = _getStrings();
        Text = s.AppTitle;
        if (_startupOverlay.Visible)
        {
            if (_startupOverlay.IsErrorState)
            {
                _startupOverlay.UpdateErrorTexts(GetFailureMessage(s, _failureKind), s.RetryButton);
            }
            else
            {
                _startupOverlay.UpdateLoadingText(s.StartupLoading);
            }
        }
    }

    private void OnCoreProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (IsDisposed) return;
        _processFailed = true;
        ShowStartupError(StartupFailureKind.Generic);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (IsDisposed) return;
        if (!e.IsSuccess)
        {
            ShowStartupError(StartupFailureKind.Generic);
        }
    }

    private void OnContainsFullScreenElementChanged(object? sender, object e)
    {
        if (IsDisposed || _webView.CoreWebView2 is null) return;
        bool wantFullscreen = _webView.CoreWebView2.ContainsFullScreenElement;
        if (wantFullscreen == _isFullscreen) return;

        _isFullscreen = wantFullscreen;
        if (wantFullscreen)
        {
            _preFullscreenWindowState = WindowState;
            _preFullscreenBorderStyle = FormBorderStyle;
            _preFullscreenBounds = Bounds;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            FormBorderStyle = _preFullscreenBorderStyle;
            Bounds = _preFullscreenBounds;
            WindowState = _preFullscreenWindowState;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? rawJson = null;
        try
        {
            rawJson = e.TryGetWebMessageAsString();
        }
        catch
        {
            rawJson = e.WebMessageAsJson;
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("action", out JsonElement actionProp)) return;
            string? action = actionProp.GetString();
            if (string.IsNullOrWhiteSpace(action)) return;

            if (string.Equals(action, "rendererReady", StringComparison.Ordinal))
            {
                _startupTimeoutTimer.Stop();
                _failureKind = StartupFailureKind.None;
                _rendererReady = true;
                _startupOverlay.HideOverlay();
                _onRendererReady();
                if (_hostSuspended || !Visible || WindowState == FormWindowState.Minimized)
                {
                    _ = SetHostSuspendedAsync(true);
                }
                return;
            }
            if (string.Equals(action, "rendererError", StringComparison.Ordinal))
            {
                _startupTimeoutTimer.Stop();
                _rendererReady = false;
                _startupOverlay.HideOverlay();
                return;
            }

            _onWebMessage(action, root);
        }
        catch
        {
            // Ignore malformed messages.
        }
    }

    private async Task SetHostSuspendedAsync(bool suspended)
    {
        _hostSuspended = suspended;
        if (!IsRendererReady) return;
        try
        {
            await WebViewEnvironmentHost.SetHostSuspendedAsync(_webView, suspended);
        }
        catch
        {
            // Ignore transient suspend calls during teardown.
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            _ = SetHostSuspendedAsync(true);
        }
        else if (Visible)
        {
            _ = SetHostSuspendedAsync(false);
        }
    }

    public void BringToFrontAndActivate()
    {
        if (IsDisposed) return;
        if (!Visible)
        {
            Show();
        }
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        BringToFront();
        _ = SetHostSuspendedAsync(false);
    }

    public async Task PushHostStateAsync(object stateDto)
    {
        if (!IsRendererReady) return;
        try
        {
            string json = JsonSerializer.Serialize(stateDto, HostJsonOptions);
            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"if (window.obliqueHostUpdateState) window.obliqueHostUpdateState({json});");
        }
        catch
        {
            // Ignore transient script errors during close.
        }
    }

    public async Task ShowToastAsync(string message)
    {
        if (!IsRendererReady) return;
        try
        {
            string jsonMsg = JsonSerializer.Serialize(message);
            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"if (window.obliqueHostShowToast) window.obliqueHostShowToast({jsonMsg});");
        }
        catch
        {
            // Ignore transient script errors.
        }
    }

    public Task<(byte[] Bytes, string Extension)> CaptureSnapshotAsync(int width, int height)
    {
        return WebViewEnvironmentHost.CaptureSnapshotFromWebViewAsync(_webView, width, height);
    }

    public void CloseForExit()
    {
        _allowRealClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowRealClose &&
            e.CloseReason == CloseReason.UserClosing &&
            _shouldMinimizeToTray())
        {
            e.Cancel = true;
            _ = SetHostSuspendedAsync(true);
            Hide();
            _onMinimizedToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (!_allowRealClose)
        {
            _onRealClose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startupTimeoutTimer.Stop();
            _startupTimeoutTimer.Dispose();
            _startupOverlay.Dispose();
            _webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
