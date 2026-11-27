using ObliqueSeasons.Native;

namespace ObliqueSeasons.Services;

/// <summary>
/// Periodically inspects the foreground window and detects whether a fullscreen app covers any monitor.
/// </summary>
internal sealed class FullscreenWatcher : IDisposable
{
    private static readonly HashSet<string> IgnoredShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "ForegroundStaging"
    };

    private readonly System.Windows.Forms.Timer _timer;
    private bool _enabled = true;
    private bool _lastFullscreenState;
    private bool _disposed;

    public event Action<bool>? FullscreenStateChanged;

    public bool IsFullscreenActive => _lastFullscreenState;

    public FullscreenWatcher()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += OnTick;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            _timer.Stop();
            if (_lastFullscreenState)
            {
                _lastFullscreenState = false;
                FullscreenStateChanged?.Invoke(false);
            }
        }
        else if (!_disposed)
        {
            _timer.Start();
            EvaluateNow();
        }
    }

    public void EvaluateNow()
    {
        if (!_enabled || _disposed) return;

        bool current = DetectFullscreenApp();
        if (current != _lastFullscreenState)
        {
            _lastFullscreenState = current;
            FullscreenStateChanged?.Invoke(current);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        EvaluateNow();
    }

    private static bool DetectFullscreenApp()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || !NativeMethods.IsWindowVisible(fg) || NativeMethods.IsIconic(fg))
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
        {
            return false;
        }

        string className = NativeMethods.GetWindowClassNameSafe(fg);
        if (IgnoredShellClasses.Contains(className))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(fg, out NativeMethods.RECT rect))
        {
            return false;
        }

        Rectangle fgRect = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (fgRect.Width <= 0 || fgRect.Height <= 0)
        {
            return false;
        }

        foreach (Screen screen in Screen.AllScreens)
        {
            Rectangle scr = screen.Bounds;
            Rectangle overlap = Rectangle.Intersect(fgRect, scr);
            if (overlap.Width <= 0 || overlap.Height <= 0)
            {
                continue;
            }

            long overlapArea = (long)overlap.Width * overlap.Height;
            long screenArea = (long)scr.Width * scr.Height;
            if (screenArea > 0 && overlapArea >= screenArea * 0.98)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
