using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;
using ObliqueSeasons.Native;

namespace ObliqueSeasons.Controls;

/// <summary>
/// Windows 11 Fluent color palette aligned with the live system taskbar theme.
/// </summary>
internal sealed class Win11MenuPalette
{
    public bool IsDark { get; }
    public Color Background { get; }
    public Color HoverFill { get; }
    public Color HoverBorder { get; }
    public Color PressedFill { get; }
    public Color Border { get; }
    public Color Divider { get; }
    public Color TextPrimary { get; }
    public Color IconDefault { get; }
    public Color IconHover { get; }
    public Color DangerHover { get; }

    public int BorderColorRef =>
        Border.R | (Border.G << 8) | (Border.B << 16);

    private Win11MenuPalette(bool isDark)
    {
        IsDark = isDark;
        if (isDark)
        {
            Background = Color.FromArgb(0x22, 0x22, 0x26);
            HoverFill = Color.FromArgb(0x34, 0x34, 0x3A);
            HoverBorder = Color.FromArgb(0x44, 0x44, 0x4C);
            PressedFill = Color.FromArgb(0x2D, 0x2D, 0x32);
            Border = Color.FromArgb(0x3A, 0x3A, 0x40);
            Divider = Color.FromArgb(0x33, 0x33, 0x39);
            TextPrimary = Color.FromArgb(0xF4, 0xF4, 0xF6);
            IconDefault = Color.FromArgb(0xCE, 0xCE, 0xD6);
            IconHover = Color.FromArgb(0xFF, 0xFF, 0xFF);
            DangerHover = Color.FromArgb(0xF8, 0x71, 0x71);
        }
        else
        {
            Background = Color.FromArgb(0xFC, 0xFC, 0xFD);
            HoverFill = Color.FromArgb(0xEF, 0xEF, 0xF4);
            HoverBorder = Color.FromArgb(0xE3, 0xE3, 0xEA);
            PressedFill = Color.FromArgb(0xE6, 0xE6, 0xED);
            Border = Color.FromArgb(0xDC, 0xDC, 0xE3);
            Divider = Color.FromArgb(0xE8, 0xE8, 0xEE);
            TextPrimary = Color.FromArgb(0x18, 0x18, 0x1B);
            IconDefault = Color.FromArgb(0x52, 0x52, 0x5B);
            IconHover = Color.FromArgb(0x18, 0x18, 0x1B);
            DangerHover = Color.FromArgb(0xDC, 0x26, 0x26);
        }
    }

    public static readonly Win11MenuPalette Dark = new(true);
    public static readonly Win11MenuPalette Light = new(false);

    public static bool IsSystemTaskbarDark()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            object? val = key?.GetValue("SystemUsesLightTheme") ?? key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal == 0;
            }
        }
        catch
        {
            // Default to dark flyout when theme registry lookup is restricted.
        }
        return true;
    }

    public static Win11MenuPalette Current() => IsSystemTaskbarDark() ? Dark : Light;
}

/// <summary>
/// Generates the fallback dark slate-navy squircle with 4 glowing slanted light bars
/// ("寸度 · 四格斜影") system tray emblem at 4x supersampling when assets/oblique-seasons.ico is unavailable.
/// </summary>
internal static class TrayIconFactory
{
    public static Icon CreateTaskbarTrayIcon(bool isDarkTaskbar, int sizePx)
    {
        int size = Math.Clamp(sizePx, 16, 64);
        int hi = Math.Clamp(size * 4, 96, 128);

        using Bitmap bmpHi = new(hi, hi, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        System.Drawing.Imaging.BitmapData data = bmpHi.LockBits(
            new Rectangle(0, 0, hi, hi),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] buf = new byte[stride * hi];

            float cx = hi * 0.5f;
            float cy = hi * 0.5f;
            float invHalf = 2.0f / hi;
            float pxScale = 2.0f / hi;

            const float squircleR = 0.94f;
            const float nExp = 4.65f;
            float invExp = 1.0f / nExp;

            // Slanted frame (~-11.5 deg) matching the four tapering light bars
            const float slope = -0.205f;
            float rimFloor = isDarkTaskbar ? 0.14f : 0.08f;

            for (int py = 0; py < hi; py++)
            {
                int rowOff = py * stride;
                float nyp = (py + 0.5f - cy) * invHalf;

                for (int px = 0; px < hi; px++)
                {
                    float nxp = (px + 0.5f - cx) * invHalf;
                    float ax = MathF.Abs(nxp) / squircleR;
                    float ay = MathF.Abs(nyp) / squircleR;
                    float rNorm = MathF.Pow(MathF.Pow(ax, nExp) + MathF.Pow(ay, nExp), invExp);
                    float dOuter = (rNorm - 1.0f) * squircleR;

                    float alpha = SmoothStep(pxScale * 1.15f, -pxScale * 1.15f, dOuter);
                    if (alpha <= 0.001f) continue;

                    // Dark slate-navy background with subtle upper-center blue-slate ambient glow
                    float vignette = Clamp01(1.0f - (nxp * nxp + nyp * nyp) * 0.65f);
                    float topLight = Clamp01(0.55f - nyp * 0.45f);
                    float rCol = Lerp(8.0f, 18.0f, vignette * 0.7f + topLight * 0.3f);
                    float gCol = Lerp(11.0f, 24.0f, vignette * 0.7f + topLight * 0.3f);
                    float bCol = Lerp(17.0f, 36.0f, vignette * 0.7f + topLight * 0.3f);

                    // Delicate top & side cool rim highlight
                    float rimBand = SmoothStep(0.055f, 0.0f, MathF.Abs(dOuter + 0.014f));
                    float rimTopWeight = Clamp01(0.68f - nyp * 0.62f);
                    float rimBoost = rimBand * (rimFloor + 0.28f * rimTopWeight);
                    rCol = Lerp(rCol, 56.0f, rimBoost);
                    gCol = Lerp(gCol, 66.0f, rimBoost);
                    bCol = Lerp(bCol, 84.0f, rimBoost);

                    // Four glowing slanted tapering horizontal bars
                    float u = nxp;
                    float v = nyp - slope * nxp;
                    float barField = 0.0f;
                    float glowField = 0.0f;

                    for (int i = 0; i < 4; i++)
                    {
                        float barHalfW = 0.52f - i * 0.088f;
                        float barHalfH = 0.072f - i * 0.007f;
                        float barY = -0.29f + i * 0.225f;
                        float barX = -0.02f - i * 0.010f;
                        float dBar = BoxSdf(u - barX, v - barY, barHalfW, barHalfH, 0.026f);

                        float intensity = 1.0f - i * 0.23f;
                        float core = SmoothStep(pxScale * 2.2f, -pxScale * 1.4f, dBar);
                        float halo = MathF.Exp(-MathF.Max(0.0f, dBar) * 14.0f) * 0.42f;
                        barField = MathF.Max(barField, core * intensity);
                        glowField += halo * intensity;
                    }

                    float bloom = Clamp01(glowField * 0.65f);
                    rCol = Lerp(rCol, 152.0f, bloom);
                    gCol = Lerp(gCol, 178.0f, bloom);
                    bCol = Lerp(bCol, 208.0f, bloom);

                    if (barField > 0.001f)
                    {
                        float lr = Lerp(116.0f, 224.0f, barField);
                        float lg = Lerp(136.0f, 234.0f, barField);
                        float lb = Lerp(160.0f, 246.0f, barField);
                        float mixAmt = Clamp01(barField * 1.15f);
                        rCol = Lerp(rCol, lr, mixAmt);
                        gCol = Lerp(gCol, lg, mixAmt);
                        bCol = Lerp(bCol, lb, mixAmt);
                    }

                    int idx = rowOff + px * 4;
                    buf[idx + 0] = ToByte(bCol);
                    buf[idx + 1] = ToByte(gCol);
                    buf[idx + 2] = ToByte(rCol);
                    buf[idx + 3] = ToByte(alpha * 255.0f);
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        }
        finally
        {
            bmpHi.UnlockBits(data);
        }

        using Bitmap bmpOut = new(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics gOut = Graphics.FromImage(bmpOut))
        {
            gOut.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gOut.SmoothingMode = SmoothingMode.HighQuality;
            gOut.PixelOffsetMode = PixelOffsetMode.HighQuality;
            gOut.Clear(Color.Transparent);
            gOut.DrawImage(bmpHi, 0, 0, size, size);
        }

        IntPtr hIcon = bmpOut.GetHicon();
        try
        {
            using Icon borrowed = Icon.FromHandle(hIcon);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static float BoxSdf(float x, float y, float halfW, float halfH, float cornerR)
    {
        float qx = MathF.Abs(x) - (halfW - cornerR);
        float qy = MathF.Abs(y) - (halfH - cornerR);
        float ox = MathF.Max(qx, 0.0f);
        float oy = MathF.Max(qy, 0.0f);
        return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0.0f) - cornerR;
    }

    private static float Clamp01(float x) => Math.Clamp(x, 0.0f, 1.0f);

    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);

    internal static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        float d = Math.Max(0.1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
        GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Custom borderless Windows 11 Fluent tray flyout with PerMonitorV2 layout and transitions-polish motion tokens.
/// </summary>
internal sealed class Win11TrayFlyoutForm : Form
{
    private const double OpenDurationMs = 250.0;   // --duration-fast
    private const double CloseDurationMs = 150.0;  // --duration-quick (open/close asymmetry)
    private const double HoverDurationMs = 150.0;  // --duration-quick
    private const double StaggerMs = 40.0;         // --duration-stagger
    private const float SlideDistanceDip = 8f;     // --distance-base
    private const float ItemDistanceDip = 4f;      // --distance-micro

    private enum MotionPhase
    {
        Idle,
        Opening,
        Open,
        Closing
    }

    private readonly Action _onOpenSettings;
    private readonly Action _onExit;
    private readonly System.Windows.Forms.Timer _animTimer;

    private Win11MenuPalette _palette;
    private Font _labelFont;
    private string _settingsLabel;
    private string _exitLabel;

    private int _menuWidth;
    private int _menuHeight;
    private int _pad;
    private int _rowHeight;
    private int _dividerY;
    private int _textXOffset;
    private int _rightPad;
    private float _dpiScale = 1f;
    private readonly Rectangle[] _rowRects = new Rectangle[2];

    private int _hoveredIndex = -1;
    private int _pressedIndex = -1;
    private bool _openUpward = true;
    private int _targetX;
    private int _targetY;

    // Surface open/close animation state
    private MotionPhase _phase = MotionPhase.Idle;
    private long _surfaceStartTs;
    private double _surfaceFromOpacity;
    private double _surfaceFromY;

    // Sliding hover pill animation state (--duration-quick, zero hover-out delay)
    private float _pillAlpha;
    private float _pillFromAlpha;
    private float _pillTargetAlpha;
    private float _pillY;
    private float _pillFromY;
    private float _pillTargetY;
    private long _pillStartTs;
    private bool _pillAnimating;

    // Row reveal offsets driven by opening timer
    private double _openElapsedMs = OpenDurationMs;
    private Action? _pendingActionOnClose;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= unchecked((int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST));
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => false;

    public Win11TrayFlyoutForm(
        string settingsLabel,
        string exitLabel,
        Action onOpenSettings,
        Action onExit)
    {
        _settingsLabel = settingsLabel;
        _exitLabel = exitLabel;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;
        _palette = Win11MenuPalette.Current();
        _labelFont = CreateUiFont();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(1, 1);
        KeyPreview = true;
        BackColor = _palette.Background;
        Opacity = 0.0;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _animTimer = new System.Windows.Forms.Timer { Interval = 12 };
        _animTimer.Tick += OnAnimTick;

        UpdateMetrics();
    }

    public void SetLabels(string settingsLabel, string exitLabel)
    {
        _settingsLabel = settingsLabel;
        _exitLabel = exitLabel;
        UpdateMetrics();
        if (Visible)
        {
            Invalidate();
        }
    }

    public void ShowAt(Point anchorScreenPoint)
    {
        _palette = Win11MenuPalette.Current();
        BackColor = _palette.Background;

        Screen screen = Screen.FromPoint(anchorScreenPoint);
        Rectangle work = screen.WorkingArea;

        // Place inside target monitor before Handle/DeviceDpi measurement so PerMonitorV2 scales accurately.
        if (!IsHandleCreated)
        {
            Opacity = 0.0;
            Location = new Point(work.Left, work.Top);
            CreateHandle();
        }

        UpdateMetrics();

        int gap = (int)Math.Round(8f * _dpiScale);
        int margin = (int)Math.Round(8f * _dpiScale);

        _targetX = Math.Clamp(
            anchorScreenPoint.X - _menuWidth / 2,
            work.Left + margin,
            Math.Max(work.Left + margin, work.Right - _menuWidth - margin));

        _openUpward = anchorScreenPoint.Y >= work.Top + work.Height / 2;
        _targetY = _openUpward
            ? Math.Clamp(
                anchorScreenPoint.Y - _menuHeight - gap,
                work.Top + margin,
                Math.Max(work.Top + margin, work.Bottom - _menuHeight - margin))
            : Math.Clamp(
                anchorScreenPoint.Y + gap,
                work.Top + margin,
                Math.Max(work.Top + margin, work.Bottom - _menuHeight - margin));

        Size = new Size(_menuWidth, _menuHeight);
        NativeMethods.ApplyWin11MenuStyle(Handle, _palette.IsDark, _palette.BorderColorRef);

        _hoveredIndex = -1;
        _pressedIndex = -1;
        _pillAlpha = 0f;
        _pillFromAlpha = 0f;
        _pillTargetAlpha = 0f;
        _pillAnimating = false;
        _pendingActionOnClose = null;

        float slideDist = SlideDistanceDip * _dpiScale;
        float startY = _openUpward ? _targetY + slideDist : _targetY - slideDist;

        if (!SystemInformation.UIEffectsEnabled)
        {
            Location = new Point(_targetX, _targetY);
            Opacity = 1.0;
            _openElapsedMs = OpenDurationMs;
            _phase = MotionPhase.Open;
            if (!Visible) Show();
        }
        else
        {
            _surfaceStartTs = Stopwatch.GetTimestamp();
            _surfaceFromOpacity = 0.0;
            _surfaceFromY = startY;
            _openElapsedMs = 0.0;
            _phase = MotionPhase.Opening;
            Opacity = 0.0;
            Location = new Point(_targetX, (int)Math.Round(startY));
            if (!Visible) Show();
            _animTimer.Start();
        }

        NativeMethods.SetForegroundWindow(Handle);
        Activate();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyWin11MenuStyle(Handle, _palette.IsDark, _palette.BorderColorRef);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateMetrics();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (_phase is MotionPhase.Opening or MotionPhase.Open)
        {
            BeginClose(null);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_phase == MotionPhase.Closing) return;

        int nextHover = HitTestRow(e.Location);
        if (nextHover != _hoveredIndex)
        {
            SetHoveredRow(nextHover);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex != -1 && _phase != MotionPhase.Closing)
        {
            SetHoveredRow(-1);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _phase == MotionPhase.Closing) return;

        int idx = HitTestRow(e.Location);
        if (idx >= 0)
        {
            _pressedIndex = idx;
            if (_hoveredIndex != idx)
            {
                SetHoveredRow(idx);
            }
            else
            {
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _phase == MotionPhase.Closing)
        {
            _pressedIndex = -1;
            return;
        }

        int idx = HitTestRow(e.Location);
        int pressed = _pressedIndex;
        _pressedIndex = -1;
        Invalidate();

        if (idx >= 0 && (idx == pressed || pressed == -1))
        {
            TriggerRow(idx);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_phase == MotionPhase.Closing) return;

        switch (e.KeyCode)
        {
            case Keys.Escape:
                e.Handled = true;
                BeginClose(null);
                break;

            case Keys.Down:
                e.Handled = true;
                SetHoveredRow(_hoveredIndex < 0 ? 0 : (_hoveredIndex + 1) % 2);
                break;

            case Keys.Up:
                e.Handled = true;
                SetHoveredRow(_hoveredIndex < 0 ? 1 : (_hoveredIndex + 1) % 2);
                break;

            case Keys.Enter:
            case Keys.Space:
                if (_hoveredIndex >= 0)
                {
                    e.Handled = true;
                    TriggerRow(_hoveredIndex);
                }
                break;
        }
    }

    private void TriggerRow(int index)
    {
        Action action = index == 0 ? _onOpenSettings : _onExit;
        BeginClose(action);
    }

    private void BeginClose(Action? afterClose)
    {
        if (afterClose is not null)
        {
            _pendingActionOnClose = afterClose;
        }

        if (_phase == MotionPhase.Idle)
        {
            RunPendingAction();
            return;
        }

        if (!SystemInformation.UIEffectsEnabled)
        {
            _animTimer.Stop();
            _phase = MotionPhase.Idle;
            Opacity = 0.0;
            Hide();
            RunPendingAction();
            return;
        }

        if (_phase == MotionPhase.Closing) return;

        _phase = MotionPhase.Closing;
        _surfaceStartTs = Stopwatch.GetTimestamp();
        _surfaceFromOpacity = Opacity;
        _surfaceFromY = Location.Y;
        _animTimer.Start();
    }

    private void RunPendingAction()
    {
        Action? action = _pendingActionOnClose;
        _pendingActionOnClose = null;
        action?.Invoke();
    }

    private void SetHoveredRow(int nextIndex)
    {
        int prevIndex = _hoveredIndex;
        _hoveredIndex = nextIndex;

        if (!SystemInformation.UIEffectsEnabled)
        {
            _pillAnimating = false;
            if (nextIndex >= 0)
            {
                _pillY = _rowRects[nextIndex].Top;
                _pillAlpha = 1f;
            }
            else
            {
                _pillAlpha = 0f;
            }
            Invalidate();
            return;
        }

        if (nextIndex >= 0)
        {
            float targetY = _rowRects[nextIndex].Top;
            if (prevIndex < 0 && _pillAlpha < 0.05f)
            {
                // Snap Y directly on fresh hover-in so pill fades straight into the cursor row.
                _pillY = targetY;
                _pillFromY = targetY;
            }
            else
            {
                _pillFromY = _pillY;
            }
            _pillTargetY = targetY;
            _pillFromAlpha = _pillAlpha;
            _pillTargetAlpha = 1f;
        }
        else
        {
            // Hover-out uses zero delay for instant responsiveness.
            _pillFromY = _pillY;
            _pillTargetY = _pillY;
            _pillFromAlpha = _pillAlpha;
            _pillTargetAlpha = 0f;
        }

        _pillStartTs = Stopwatch.GetTimestamp();
        _pillAnimating = true;
        _animTimer.Start();
        Invalidate();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        bool needMoreTicks = false;

        if (_phase == MotionPhase.Opening)
        {
            double elapsed = ElapsedMs(_surfaceStartTs, now);
            double t = Math.Clamp(elapsed / OpenDurationMs, 0.0, 1.0);
            double eased = EaseSmoothOut(t);

            _openElapsedMs = elapsed;
            Opacity = _surfaceFromOpacity + (1.0 - _surfaceFromOpacity) * eased;
            int nextY = (int)Math.Round(_surfaceFromY + (_targetY - _surfaceFromY) * eased);
            if (Location.Y != nextY)
            {
                Location = new Point(_targetX, nextY);
            }

            if (t >= 1.0)
            {
                Opacity = 1.0;
                Location = new Point(_targetX, _targetY);
                _openElapsedMs = OpenDurationMs;
                _phase = MotionPhase.Open;
            }
            else
            {
                needMoreTicks = true;
            }
            Invalidate();
        }
        else if (_phase == MotionPhase.Closing)
        {
            double elapsed = ElapsedMs(_surfaceStartTs, now);
            double t = Math.Clamp(elapsed / CloseDurationMs, 0.0, 1.0);
            double eased = EaseSmoothOut(t);

            // Quieter asymmetric close: shorter 150ms fade with tiny 4px drift.
            float closeDrift = ItemDistanceDip * _dpiScale * (_openUpward ? 1f : -1f);
            Opacity = Math.Max(0.0, _surfaceFromOpacity * (1.0 - eased));
            int nextY = (int)Math.Round(_surfaceFromY + closeDrift * eased);
            if (Location.Y != nextY)
            {
                Location = new Point(_targetX, nextY);
            }

            if (t >= 1.0)
            {
                _phase = MotionPhase.Idle;
                _pillAnimating = false;
                Opacity = 0.0;
                _animTimer.Stop();
                Hide();
                RunPendingAction();
                return;
            }
            needMoreTicks = true;
        }

        if (_pillAnimating)
        {
            double elapsed = ElapsedMs(_pillStartTs, now);
            double t = Math.Clamp(elapsed / HoverDurationMs, 0.0, 1.0);
            double eased = (float)EaseSmoothOut(t);

            _pillAlpha = (float)(_pillFromAlpha + (_pillTargetAlpha - _pillFromAlpha) * eased);
            _pillY = (float)(_pillFromY + (_pillTargetY - _pillFromY) * eased);

            if (t >= 1.0)
            {
                _pillAlpha = _pillTargetAlpha;
                _pillY = _pillTargetY;
                _pillAnimating = false;
            }
            else
            {
                needMoreTicks = true;
            }
            Invalidate();
        }

        if (!needMoreTicks)
        {
            _animTimer.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(_palette.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Color activeFill = _pressedIndex >= 0 && _pressedIndex == _hoveredIndex
            ? _palette.PressedFill
            : _palette.HoverFill;

        if (_pillAlpha > 0.01f)
        {
            RectangleF pillRect = new(_pad, _pillY, Math.Max(1, _menuWidth - _pad * 2), _rowHeight);
            float radius = 6f * _dpiScale;
            Color pillFill = Blend(_palette.Background, activeFill, _pillAlpha);
            Color pillBorder = Blend(_palette.Background, _palette.HoverBorder, _pillAlpha);

            using GraphicsPath path = TrayIconFactory.CreateRoundedRectPath(pillRect, radius);
            using SolidBrush fillBrush = new(pillFill);
            using Pen borderPen = new(pillBorder, 1f);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);
        }

        int divInset = (int)Math.Round(10f * _dpiScale);
        using (Pen sepPen = new(_palette.Divider, 1f))
        {
            g.DrawLine(sepPen, _pad + divInset, _dividerY, _menuWidth - _pad - divInset, _dividerY);
        }

        for (int i = 0; i < 2; i++)
        {
            Rectangle row = _rowRects[i];
            float rowOffsetY = ComputeRowStaggerOffsetY(i);
            float rowPillMix = Math.Clamp(1f - Math.Abs(_pillY - row.Top) / Math.Max(1f, _rowHeight), 0f, 1f) * _pillAlpha;
            Color cellBack = Blend(_palette.Background, activeFill, rowPillMix);

            Color glyphColor = i == 1
                ? Blend(_palette.IconDefault, _palette.DangerHover, rowPillMix)
                : Blend(_palette.IconDefault, _palette.IconHover, rowPillMix);

            float cx = _pad + 17f * _dpiScale;
            float cy = row.Top + row.Height * 0.5f + rowOffsetY;
            if (i == 0)
            {
                DrawFluentGear(g, cx, cy, _dpiScale, glyphColor);
            }
            else
            {
                DrawFluentExit(g, cx, cy, _dpiScale, glyphColor);
            }

            string text = i == 0 ? _settingsLabel : _exitLabel;
            Rectangle textRect = new(
                _pad + _textXOffset,
                row.Top + (int)Math.Round(rowOffsetY),
                Math.Max(10, _menuWidth - _pad * 2 - _textXOffset - _rightPad),
                row.Height);

            // Pass exact solid cellBack so GDI ClearType renders crisp text with zero edge halo.
            TextRenderer.DrawText(
                g,
                text,
                _labelFont,
                textRect,
                _palette.TextPrimary,
                cellBack,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    private float ComputeRowStaggerOffsetY(int index)
    {
        if (_phase != MotionPhase.Opening || !SystemInformation.UIEffectsEnabled)
        {
            return 0f;
        }

        double delay = index * StaggerMs;
        double localT = Math.Clamp((_openElapsedMs - delay) / Math.Max(1.0, OpenDurationMs - delay), 0.0, 1.0);
        double eased = EaseSmoothOut(localT);
        float sign = _openUpward ? 1f : -1f;
        return (float)((1.0 - eased) * ItemDistanceDip * _dpiScale * sign);
    }

    private int HitTestRow(Point pt)
    {
        for (int i = 0; i < _rowRects.Length; i++)
        {
            if (_rowRects[i].Contains(pt))
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateMetrics()
    {
        _dpiScale = Math.Max(1f, DeviceDpi / 96f);
        _pad = (int)Math.Round(5f * _dpiScale);
        _rowHeight = (int)Math.Round(34f * _dpiScale);
        int divHeight = (int)Math.Round(9f * _dpiScale);
        _textXOffset = (int)Math.Round(34f * _dpiScale);
        _rightPad = (int)Math.Round(18f * _dpiScale);

        Size s0;
        Size s1;
        if (IsHandleCreated)
        {
            using Graphics gm = CreateGraphics();
            s0 = TextRenderer.MeasureText(gm, _settingsLabel, _labelFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            s1 = TextRenderer.MeasureText(gm, _exitLabel, _labelFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        else
        {
            s0 = TextRenderer.MeasureText(_settingsLabel, _labelFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            s1 = TextRenderer.MeasureText(_exitLabel, _labelFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        int maxTextW = Math.Max(s0.Width, s1.Width);

        _menuWidth = Math.Max((int)Math.Round(152f * _dpiScale), _pad * 2 + _textXOffset + maxTextW + _rightPad);
        _menuHeight = _pad * 2 + _rowHeight * 2 + divHeight;

        _rowRects[0] = new Rectangle(_pad, _pad, _menuWidth - _pad * 2, _rowHeight);
        _dividerY = _pad + _rowHeight + divHeight / 2;
        _rowRects[1] = new Rectangle(_pad, _pad + _rowHeight + divHeight, _menuWidth - _pad * 2, _rowHeight);

        Size = new Size(_menuWidth, _menuHeight);
    }

    private static Font CreateUiFont()
    {
        using InstalledFontCollection fonts = new();
        bool hasVariable = false;
        bool hasYaHei = false;
        foreach (FontFamily ff in fonts.Families)
        {
            if (string.Equals(ff.Name, "Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase))
            {
                hasVariable = true;
            }
            else if (string.Equals(ff.Name, "Microsoft YaHei UI", StringComparison.OrdinalIgnoreCase))
            {
                hasYaHei = true;
            }
        }

        string family = hasVariable
            ? "Segoe UI Variable Text"
            : (hasYaHei ? "Microsoft YaHei UI" : (SystemFonts.MessageBoxFont?.FontFamily.Name ?? "Segoe UI"));
        return new Font(family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static void DrawFluentGear(Graphics g, float cx, float cy, float s, Color color)
    {
        float stroke = Math.Max(1.2f, 1.3f * s);
        using Pen pen = new(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        float rInner = 2.0f * s;
        float rOuter = 4.1f * s;
        float rTooth = 5.45f * s;

        g.DrawEllipse(pen, cx - rOuter, cy - rOuter, rOuter * 2f, rOuter * 2f);
        g.DrawEllipse(pen, cx - rInner, cy - rInner, rInner * 2f, rInner * 2f);

        for (int k = 0; k < 6; k++)
        {
            double angle = k * (Math.PI / 3.0) + (Math.PI / 6.0);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            g.DrawLine(pen, cx + cos * (rOuter - 0.1f * s), cy + sin * (rOuter - 0.1f * s), cx + cos * rTooth, cy + sin * rTooth);
        }
    }

    private static void DrawFluentExit(Graphics g, float cx, float cy, float s, Color color)
    {
        float stroke = Math.Max(1.2f, 1.3f * s);
        using Pen pen = new(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLines(pen,
        [
            new PointF(cx + 0.5f * s, cy - 4.7f * s),
            new PointF(cx - 4.2f * s, cy - 4.7f * s),
            new PointF(cx - 4.2f * s, cy + 4.7f * s),
            new PointF(cx + 0.5f * s, cy + 4.7f * s)
        ]);
        g.DrawLine(pen, cx - 0.8f * s, cy, cx + 4.8f * s, cy);
        g.DrawLines(pen,
        [
            new PointF(cx + 2.4f * s, cy - 2.3f * s),
            new PointF(cx + 4.8f * s, cy),
            new PointF(cx + 2.4f * s, cy + 2.3f * s)
        ]);
    }

    private static double ElapsedMs(long startTs, long nowTs)
    {
        return (nowTs - startTs) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Exact cubic-bezier(0.22, 1, 0.36, 1) solver (--ease-smooth-out).
    /// </summary>
    private static double EaseSmoothOut(double x)
    {
        if (x <= 0.0) return 0.0;
        if (x >= 1.0) return 1.0;

        const double x1 = 0.22, y1 = 1.0, x2 = 0.36, y2 = 1.0;
        double u = x;
        for (int i = 0; i < 6; i++)
        {
            double inv = 1.0 - u;
            double bx = 3.0 * inv * inv * u * x1 + 3.0 * inv * u * u * x2 + u * u * u;
            double dbx = 3.0 * inv * inv * x1 + 6.0 * inv * u * (x2 - x1) + 3.0 * u * u * (1.0 - x2);
            if (Math.Abs(dbx) < 1e-6) break;
            u = Math.Clamp(u - (bx - x) / dbx, 0.0, 1.0);
        }

        double mu = 1.0 - u;
        return Math.Clamp(3.0 * mu * mu * u * y1 + 3.0 * mu * u * u * y2 + u * u * u, 0.0, 1.0);
    }

    private static Color Blend(Color from, Color to, float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        int r = (int)Math.Round(from.R + (to.R - from.R) * k);
        int g = (int)Math.Round(from.G + (to.G - from.G) * k);
        int b = (int)Math.Round(from.B + (to.B - from.B) * k);
        return Color.FromArgb(255, Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Stop();
            _animTimer.Dispose();
            _labelFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
