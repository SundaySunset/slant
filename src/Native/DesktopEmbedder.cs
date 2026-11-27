namespace ObliqueSeasons.Native;

/// <summary>
/// Embeds a borderless host window behind the Windows desktop icons across
/// both Legacy WorkerW (Win10 / Win11 &lt;= 23H2) and RaisedDesktop (Win11 24H2 / 25H2+).
/// </summary>
internal static class DesktopEmbedder
{
    public enum EmbedStrategy
    {
        None,
        RaisedDesktopProgman,
        LegacyWorkerW,
        BottomFallback
    }

    public static EmbedStrategy Embed(IntPtr hwnd, Rectangle virtualBounds)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return EmbedStrategy.None;
        }

        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            NativeMethods.SendMessageTimeout(
                progman, 0x052C, new IntPtr(0xD), new IntPtr(1),
                NativeMethods.SMTO_NORMAL, 1000, out _);
            NativeMethods.SendMessageTimeout(
                progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SMTO_NORMAL, 1000, out _);
        }

        IntPtr progmanDefView = progman != IntPtr.Zero
            ? NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null)
            : IntPtr.Zero;
        IntPtr progmanWorkerW = progman != IntPtr.Zero
            ? NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null)
            : IntPtr.Zero;

        long baseStyle = NativeMethods.GetWindowLongPtrSafe(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        baseStyle &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME |
                       NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX |
                       NativeMethods.WS_SYSMENU | NativeMethods.WS_BORDER | NativeMethods.WS_DLGFRAME);

        long baseExStyle = NativeMethods.GetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        baseExStyle &= ~(NativeMethods.WS_EX_DLGMODALFRAME | NativeMethods.WS_EX_WINDOWEDGE |
                         NativeMethods.WS_EX_CLIENTEDGE | NativeMethods.WS_EX_STATICEDGE |
                         NativeMethods.WS_EX_APPWINDOW);
        baseExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;

        if (progman != IntPtr.Zero && progmanDefView != IntPtr.Zero && progmanWorkerW != IntPtr.Zero)
        {
            long style = (baseStyle | NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE) & ~NativeMethods.WS_POPUP;
            long exStyle = baseExStyle | NativeMethods.WS_EX_LAYERED;

            NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));
            NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, 255, NativeMethods.LWA_ALPHA);
            NativeMethods.SetParent(hwnd, progman);

            NativeMethods.POINT pt = new() { X = virtualBounds.X, Y = virtualBounds.Y };
            NativeMethods.MapWindowPoints(IntPtr.Zero, progman, ref pt, 1);

            NativeMethods.SetWindowPos(
                hwnd, progmanDefView,
                pt.X, pt.Y, virtualBounds.Width, virtualBounds.Height,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
            return EmbedStrategy.RaisedDesktopProgman;
        }

        IntPtr legacyWorkerW = FindLegacyWorkerW();
        if (legacyWorkerW != IntPtr.Zero)
        {
            long style = (baseStyle | NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE) & ~NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));
            NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(baseExStyle));
            NativeMethods.SetParent(hwnd, legacyWorkerW);

            NativeMethods.POINT pt = new() { X = virtualBounds.X, Y = virtualBounds.Y };
            NativeMethods.MapWindowPoints(IntPtr.Zero, legacyWorkerW, ref pt, 1);

            NativeMethods.SetWindowPos(
                hwnd, IntPtr.Zero,
                pt.X, pt.Y, virtualBounds.Width, virtualBounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED |
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
            return EmbedStrategy.LegacyWorkerW;
        }

        long fallbackStyle = (baseStyle | NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE) & ~NativeMethods.WS_CHILD;
        NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_STYLE, new IntPtr(fallbackStyle));
        NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(baseExStyle));
        NativeMethods.SetParent(hwnd, IntPtr.Zero);
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_BOTTOM,
            virtualBounds.X, virtualBounds.Y, virtualBounds.Width, virtualBounds.Height,
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        return EmbedStrategy.BottomFallback;
    }

    public static void DetachAndRefresh(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
            NativeMethods.SetParent(hwnd, IntPtr.Zero);
        }

        NativeMethods.InvalidateRect(IntPtr.Zero, IntPtr.Zero, true);
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            NativeMethods.RedrawWindow(
                progman, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE |
                NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_UPDATENOW);
        }
    }

    private static IntPtr FindLegacyWorkerW()
    {
        IntPtr workerW = IntPtr.Zero;
        NativeMethods.EnumWindows((topHandle, _) =>
        {
            IntPtr defView = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                workerW = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return workerW;
    }
}
