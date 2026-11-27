using System.Text.Json;
using Microsoft.Win32;
using ObliqueSeasons.Controls;
using ObliqueSeasons.Forms;
using ObliqueSeasons.Models;
using ObliqueSeasons.Native;
using ObliqueSeasons.Services;

namespace ObliqueSeasons;

/// <summary>
/// Coordinates tray icon, live desktop wallpaper window, control panel window,
/// static desktop/lock-screen snapshot application, fullscreen pause, and scheduled sync.
/// </summary>
internal sealed class WallpaperAppContext : ApplicationContext
{
    public static readonly uint WmShowMe = NativeMethods.RegisterWindowMessage("ObliqueSeasons_WM_SHOWME_V2");
    private static readonly uint WmTaskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private sealed class HostMessageWindow : NativeWindow, IDisposable
    {
        private readonly Action _onShowMe;
        private readonly Action _onTaskbarCreated;

        public HostMessageWindow(Action onShowMe, Action onTaskbarCreated)
        {
            _onShowMe = onShowMe;
            _onTaskbarCreated = onTaskbarCreated;
            CreateHandle(new CreateParams
            {
                Caption = "ObliqueSeasonsHostMessageSink"
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (WmShowMe != 0 && m.Msg == unchecked((int)WmShowMe))
            {
                _onShowMe();
                return;
            }
            if (WmTaskbarCreated != 0 && m.Msg == unchecked((int)WmTaskbarCreated))
            {
                _onTaskbarCreated();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }

    private sealed class HostStateDto
    {
        public string DesktopMode { get; init; } = "off";
        public bool AutoSyncLockScreen { get; init; }
        public int SyncIntervalMinutes { get; init; }
        public bool AutoStart { get; init; }
        public bool PauseOnFullscreenApp { get; init; }
        public bool MinimizeToTrayOnClose { get; init; }
        public string LanguageMode { get; init; } = "auto";
        public string ResolvedLanguage { get; init; } = "zh";
        public bool HasOriginalWallpaperBackup { get; init; }
        public bool HasOriginalLockScreenBackup { get; init; }
        public bool CanRestoreDesktop { get; init; }
        public bool CanRestoreLockScreen { get; init; }
        public bool CanRestoreSystem { get; init; }
        public string NativeResolution { get; init; } = string.Empty;
        public string LastLockScreenSync { get; init; } = string.Empty;
        public string LastDesktopSync { get; init; } = string.Empty;
        public bool Busy { get; init; }
        public bool FullscreenPaused { get; init; }
    }

    private readonly AppSettings _settings;
    private readonly Icon _appIcon;
    private Icon _trayIconImage;
    private readonly NotifyIcon _trayIcon;
    private readonly Win11TrayFlyoutForm _trayFlyout;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly FullscreenWatcher _fullscreenWatcher;
    private readonly System.Windows.Forms.Timer _lockSyncTimer;
    private readonly HostMessageWindow _messageWindow;

    private readonly bool _launchedMinimized;
    private MainControlForm? _mainForm;
    private DesktopWallpaperForm? _desktopForm;
    private bool _autoSyncArmed;
    private bool _isBusy;
    private bool _trayHintShown;
    private bool _disposed;

    private Localization.HostStrings S => Localization.Get(_settings.LanguageMode);

    public WallpaperAppContext(bool startMinimized, bool windowOnlyMode)
    {
        _settings = AppSettings.Load();
        WebViewEnvironmentHost.PrewarmEnvironment();
        _launchedMinimized = startMinimized;
        _autoSyncArmed = startMinimized;
        _appIcon = LoadAppIcon();
        _trayIconImage = CreateThemeAwareTrayIcon();
        Localization.HostStrings s = S;

        _trayFlyout = new Win11TrayFlyoutForm(
            s.MenuOpenMain,
            s.MenuExit,
            onOpenSettings: ShowMainWindow,
            onExit: ExitApplication);

        _trayIcon = new NotifyIcon
        {
            Text = s.AppTitle,
            Icon = _trayIconImage,
            Visible = true
        };
        _trayIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && !_disposed)
            {
                _trayFlyout.ShowAt(Cursor.Position);
            }
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        _fullscreenWatcher = new FullscreenWatcher();
        _fullscreenWatcher.FullscreenStateChanged += OnFullscreenStateChanged;
        _fullscreenWatcher.SetEnabled(_settings.PauseOnFullscreenApp);

        _lockSyncTimer = new System.Windows.Forms.Timer();
        _lockSyncTimer.Tick += async (_, _) =>
        {
            try
            {
                await ApplyLockScreenAsync(isAutoSync: true);
            }
            catch
            {
                // Auto-sync timer failures must not crash the tray host.
            }
        };
        ConfigureLockSyncTimer();

        _messageWindow = new HostMessageWindow(
            onShowMe: ShowMainWindow,
            onTaskbarCreated: OnExplorerRestarted);

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;

        // Only mount live desktop wallpaper automatically during background/autostart launches.
        // Interactive launches let the user preview first and click to apply.
        if (startMinimized && !windowOnlyMode && _settings.DesktopMode == "live")
        {
            EnsureDesktopFormRunning();
        }

        if (!startMinimized)
        {
            ShowMainWindow();
        }
        else if (_settings.DesktopMode == "live")
        {
            _trayIcon.ShowBalloonTip(
                2400,
                s.AppTitle,
                s.BalloonStartedBackground,
                ToolTipIcon.None);
        }
    }

    private static Icon CreateThemeAwareTrayIcon()
    {
        int sizePx = Math.Clamp(SystemInformation.SmallIconSize.Width, 16, 64);
        try
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "assets", "oblique-seasons.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath, sizePx, sizePx);
            }
        }
        catch
        {
            // Fall back to procedural icon if .ico cannot be loaded at tray size.
        }

        bool darkTaskbar = Win11MenuPalette.IsSystemTaskbarDark();
        return TrayIconFactory.CreateTaskbarTrayIcon(darkTaskbar, sizePx);
    }

    private void RefreshTrayIconForTheme()
    {
        if (_disposed) return;
        Icon next = CreateThemeAwareTrayIcon();
        Icon old = _trayIconImage;
        _trayIconImage = next;
        _trayIcon.Icon = next;
        old.Dispose();
    }

    private Icon LoadAppIcon()
    {
        try
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "assets", "oblique-seasons.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }
        }
        catch
        {
            // Fall back to executable icon.
        }

        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                Icon? extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null) return extracted;
            }
        }
        catch
        {
            // Fall back to system application icon.
        }

        return SystemIcons.Application;
    }

    private void ShowMainWindow()
    {
        if (_disposed) return;

        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainControlForm(
                _appIcon,
                getStrings: () => S,
                shouldMinimizeToTray: () => _settings.MinimizeToTrayOnClose,
                onMinimizedToTray: OnMainFormMinimizedToTray,
                onRealClose: () => { if (!_disposed) ExitApplication(); },
                onWebMessage: HandleWebMessage,
                onRendererReady: async () => await PushStateToMainFormAsync());
        }

        _mainForm.UpdateLocalizedTitle(S.AppTitle);
        _mainForm.BringToFrontAndActivate();
    }

    private void OnMainFormMinimizedToTray()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        Localization.HostStrings s = S;
        _trayIcon.ShowBalloonTip(
            2200,
            s.AppTitle,
            s.BalloonMinimized,
            ToolTipIcon.None);
    }

    private void EnsureDesktopFormRunning()
    {
        if (_disposed) return;
        if (_desktopForm is null || _desktopForm.IsDisposed)
        {
            _desktopForm = new DesktopWallpaperForm();
            _desktopForm.Show();
            _desktopForm.ReEmbed();
            if (_settings.PauseOnFullscreenApp && _fullscreenWatcher.IsFullscreenActive)
            {
                _ = _desktopForm.SetHostSuspendedAsync(true);
            }
        }
        else
        {
            _desktopForm.ReEmbed();
        }
    }

    private void CloseDesktopForm()
    {
        if (_desktopForm is null) return;
        DesktopWallpaperForm form = _desktopForm;
        _desktopForm = null;
        if (!form.IsDisposed)
        {
            form.DetachNow();
            form.Close();
            form.Dispose();
        }
    }

    private string GetEffectiveDesktopMode()
    {
        if (_settings.DesktopMode == "live")
        {
            return _desktopForm is { IsDisposed: false } ? "live" : "off";
        }
        return _settings.DesktopMode;
    }

    private void RefreshDesktopEmbed()
    {
        string effectiveMode = GetEffectiveDesktopMode();
        if (effectiveMode == "live")
        {
            EnsureDesktopFormRunning();
            NotifyUser(S.ToastRefreshedEmbed);
        }
        else if (effectiveMode == "static")
        {
            _ = ApplyStaticDesktopAsync();
        }
        else
        {
            NotifyUser(S.ToastDesktopOffInfo);
        }
    }

    private void ConfigureLockSyncTimer()
    {
        _lockSyncTimer.Stop();
        if (_settings.AutoSyncLockScreen && _autoSyncArmed)
        {
            int minutes = _settings.SyncIntervalMinutes is 15 or 30 or 60 ? _settings.SyncIntervalMinutes : 30;
            _lockSyncTimer.Interval = minutes * 60 * 1000;
            _lockSyncTimer.Start();
        }
    }

    private void ApplyLocalization()
    {
        Localization.HostStrings s = S;
        _trayIcon.Text = s.AppTitle;
        _trayFlyout.SetLabels(s.MenuOpenMain, s.MenuExit);

        if (_mainForm is not null && !_mainForm.IsDisposed)
        {
            _mainForm.RefreshLocalization();
        }
    }

    private HostStateDto BuildHostStateDto()
    {
        Localization.HostStrings s = S;
        (int w, int h) = NativeMethods.GetPrimaryMonitorPhysicalResolution();
        string effectiveMode = GetEffectiveDesktopMode();
        bool hasDesktopBackup = SystemWallpaperService.HasBackup(_settings);
        bool hasLockBackup = LockScreenService.HasBackup(_settings);
        bool canRestoreDesktop = effectiveMode != "off" || hasDesktopBackup;
        bool canRestoreLock = hasLockBackup;

        return new HostStateDto
        {
            DesktopMode = effectiveMode,
            AutoSyncLockScreen = _settings.AutoSyncLockScreen,
            SyncIntervalMinutes = _settings.SyncIntervalMinutes,
            AutoStart = AutoStartService.IsEnabled(),
            PauseOnFullscreenApp = _settings.PauseOnFullscreenApp,
            MinimizeToTrayOnClose = _settings.MinimizeToTrayOnClose,
            LanguageMode = _settings.LanguageMode,
            ResolvedLanguage = Localization.ResolveLanguage(_settings.LanguageMode),
            HasOriginalWallpaperBackup = hasDesktopBackup,
            HasOriginalLockScreenBackup = hasLockBackup,
            CanRestoreDesktop = canRestoreDesktop,
            CanRestoreLockScreen = canRestoreLock,
            CanRestoreSystem = canRestoreDesktop || canRestoreLock,
            NativeResolution = $"{w} × {h}",
            LastLockScreenSync = FormatSyncTimestamp(_settings.LastLockScreenSyncIso, s.NotSynced),
            LastDesktopSync = FormatSyncTimestamp(_settings.LastDesktopSyncIso, s.NotApplied),
            Busy = _isBusy,
            FullscreenPaused = _settings.PauseOnFullscreenApp && _fullscreenWatcher.IsFullscreenActive
        };
    }

    private static string FormatSyncTimestamp(string? iso, string fallback)
    {
        if (string.IsNullOrWhiteSpace(iso)) return fallback;
        if (DateTimeOffset.TryParse(iso, out DateTimeOffset dt))
        {
            DateTime local = dt.ToLocalTime().DateTime;
            return local.Date == DateTime.Today
                ? local.ToString("HH:mm:ss")
                : local.ToString("MM-dd HH:mm");
        }
        return fallback;
    }

    private async Task PushStateToMainFormAsync()
    {
        if (_mainForm is not null && _mainForm.IsRendererReady)
        {
            await _mainForm.PushHostStateAsync(BuildHostStateDto());
        }
    }

    private async Task<bool> TryEnterOperationGateAsync(int timeoutMs = Timeout.Infinite)
    {
        if (_disposed) return false;
        try
        {
            return await _operationGate.WaitAsync(timeoutMs);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void ReleaseOperationGate()
    {
        try
        {
            _operationGate.Release();
        }
        catch (ObjectDisposedException)
        {
            // App is shutting down.
        }
        catch (SemaphoreFullException)
        {
            // Ignore double release guards.
        }
    }

    private async void NotifyUser(string message, bool showBalloonIfHidden = false)
    {
        try
        {
            if (_disposed) return;
            if (_mainForm is not null && _mainForm.IsRendererReady && _mainForm.Visible)
            {
                await _mainForm.ShowToastAsync(message);
            }
            else if (showBalloonIfHidden)
            {
                _trayIcon.ShowBalloonTip(2200, S.AppTitle, message, ToolTipIcon.None);
            }
        }
        catch
        {
            // Best-effort UI notification.
        }
    }

    private async void HandleWebMessage(string action, JsonElement payload)
    {
        try
        {
            switch (action)
            {
                case "getHostState":
                    await PushStateToMainFormAsync();
                    break;

                case "setDesktopMode":
                    if (payload.TryGetProperty("mode", out JsonElement modeEl))
                    {
                        string mode = modeEl.GetString() ?? "live";
                        await SetDesktopModeAsync(mode, showToast: true);
                    }
                    break;

                case "applyStaticDesktopNow":
                    await ApplyStaticDesktopAsync();
                    break;

                case "restoreOriginalDesktop":
                    await RestoreOriginalWallpaperAsync();
                    break;

                case "refreshDesktopEmbed":
                    RefreshDesktopEmbed();
                    break;

                case "applyLockScreenNow":
                    await ApplyLockScreenAsync(isAutoSync: false);
                    break;

                case "applyBothNow":
                    await ApplyBothAsync();
                    break;

                case "restoreOriginalLockScreen":
                    await RestoreOriginalLockScreenAsync();
                    break;

                case "restoreBoth":
                    await RestoreBothAsync();
                    break;

                case "setAutoSyncLockScreen":
                    if (payload.TryGetProperty("enabled", out JsonElement autoSyncEl))
                    {
                        await SetAutoSyncLockScreenAsync(autoSyncEl.GetBoolean());
                    }
                    break;

                case "setSyncIntervalMinutes":
                    if (payload.TryGetProperty("minutes", out JsonElement minEl) && minEl.TryGetInt32(out int mins))
                    {
                        await SetSyncIntervalMinutesAsync(mins);
                    }
                    break;

                case "setAutoStart":
                    if (payload.TryGetProperty("enabled", out JsonElement autoStartEl))
                    {
                        await SetAutoStartAsync(autoStartEl.GetBoolean());
                    }
                    break;

                case "setPauseOnFullscreen":
                    if (payload.TryGetProperty("enabled", out JsonElement pauseEl))
                    {
                        await SetPauseOnFullscreenAsync(pauseEl.GetBoolean());
                    }
                    break;

                case "setMinimizeToTray":
                    if (payload.TryGetProperty("enabled", out JsonElement minTrayEl))
                    {
                        _settings.MinimizeToTrayOnClose = minTrayEl.GetBoolean();
                        _settings.Save();
                        await PushStateToMainFormAsync();
                        NotifyUser(_settings.MinimizeToTrayOnClose
                            ? S.ToastMinimizeToTrayOn
                            : S.ToastMinimizeToTrayOff);
                    }
                    break;

                case "setLanguageMode":
                    if (payload.TryGetProperty("mode", out JsonElement langEl))
                    {
                        string langMode = langEl.GetString() ?? "auto";
                        if (langMode is "auto" or "zh" or "en")
                        {
                            _settings.LanguageMode = langMode;
                            _settings.Save();
                            ApplyLocalization();
                            await PushStateToMainFormAsync();
                        }
                    }
                    break;

                case "openLockScreenSettings":
                    OpenWindowsLockScreenSettings();
                    break;

                case "openBackgroundSettings":
                    OpenWindowsBackgroundSettings();
                    break;

                case "saveScreenshot":
                    await SaveScreenshotAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastOperationFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
    }

    private async Task<(byte[] Bytes, string Extension, int Width, int Height)> CaptureCurrentSceneAsync()
    {
        (int w, int h) = NativeMethods.GetPrimaryMonitorPhysicalResolution();

        if (_desktopForm is { IsDisposed: false, IsRendererReady: true })
        {
            (byte[] bytes, string ext) = await _desktopForm.CaptureSnapshotAsync(w, h);
            return (bytes, ext, w, h);
        }

        if (_mainForm is { IsDisposed: false, IsRendererReady: true, Visible: true } &&
            _mainForm.WindowState != FormWindowState.Minimized)
        {
            (byte[] bytes, string ext) = await _mainForm.CaptureSnapshotAsync(w, h);
            return (bytes, ext, w, h);
        }

        (byte[] offBytes, string offExt) = await OffscreenCaptureForm.CaptureOffscreenAsync(w, h);
        return (offBytes, offExt, w, h);
    }

    private async Task SetDesktopModeAsync(string mode, bool showToast)
    {
        if (mode is not ("live" or "static" or "off")) return;

        if (mode == "live")
        {
            SystemWallpaperService.EnsureOriginalWallpaperBackedUp(_settings);
            _settings.DesktopMode = "live";
            _settings.Save();
            EnsureDesktopFormRunning();
            await PushStateToMainFormAsync();
            if (showToast) NotifyUser(S.ToastDesktopLiveSet, showBalloonIfHidden: true);
        }
        else if (mode == "static")
        {
            CloseDesktopForm();
            _settings.DesktopMode = "static";
            _settings.Save();
            await PushStateToMainFormAsync();
            await ApplyStaticDesktopAsync();
        }
        else
        {
            CloseDesktopForm();
            _settings.DesktopMode = "off";
            _settings.Save();
            await PushStateToMainFormAsync();
            if (showToast) NotifyUser(S.ToastDesktopOffSet, showBalloonIfHidden: true);
        }
    }

    private async Task ApplyStaticDesktopAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            (byte[] bytes, string ext, int w, int h) = await CaptureCurrentSceneAsync();
            SystemWallpaperService.ApplyStaticWallpaper(bytes, ext, _settings);
            if (_settings.DesktopMode != "static")
            {
                CloseDesktopForm();
                _settings.DesktopMode = "static";
                _settings.Save();
            }
            NotifyUser(string.Format(S.ToastStaticAppliedFormat, w, h), showBalloonIfHidden: true);
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastStaticFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task RestoreOriginalWallpaperAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            CloseDesktopForm();
            _settings.DesktopMode = "off";
            _settings.Save();

            bool restored = SystemWallpaperService.RestoreOriginalWallpaper(_settings);
            NotifyUser(
                restored ? S.ToastRestoredOriginal : S.ToastRestoreNotFound,
                showBalloonIfHidden: true);
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastRestoreFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task ApplyLockScreenAsync(bool isAutoSync)
    {
        if (!await TryEnterOperationGateAsync(isAutoSync ? 0 : Timeout.Infinite))
        {
            return;
        }

        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            (byte[] bytes, string ext, int w, int h) = await CaptureCurrentSceneAsync();
            await LockScreenService.ApplyLockScreenAsync(bytes, ext, _settings);
            if (!isAutoSync && _settings.AutoSyncLockScreen)
            {
                _autoSyncArmed = true;
                ConfigureLockSyncTimer();
            }
            if (!isAutoSync)
            {
                NotifyUser(string.Format(S.ToastLockAppliedFormat, w, h), showBalloonIfHidden: true);
            }
        }
        catch (Exception ex)
        {
            if (!isAutoSync)
            {
                NotifyUser(
                    string.Format(S.ToastLockFailedFormat, ex.Message),
                    showBalloonIfHidden: true);
            }
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task RestoreOriginalLockScreenAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            _settings.AutoSyncLockScreen = false;
            _autoSyncArmed = false;
            ConfigureLockSyncTimer();
            _settings.Save();

            bool restored = await LockScreenService.RestoreOriginalLockScreenAsync(_settings);
            NotifyUser(
                restored ? S.ToastRestoredLock : S.ToastRestoreLockNotFound,
                showBalloonIfHidden: true);
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastRestoreLockFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task ApplyBothAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            string effectiveMode = GetEffectiveDesktopMode();
            if (effectiveMode == "off")
            {
                SystemWallpaperService.EnsureOriginalWallpaperBackedUp(_settings);
                _settings.DesktopMode = "live";
                _settings.Save();
                EnsureDesktopFormRunning();
                effectiveMode = "live";
            }

            (byte[] bytes, string ext, int w, int h) = await CaptureCurrentSceneAsync();
            if (effectiveMode == "static")
            {
                SystemWallpaperService.ApplyStaticWallpaper(bytes, ext, _settings);
            }
            await LockScreenService.ApplyLockScreenAsync(bytes, ext, _settings);
            if (_settings.AutoSyncLockScreen)
            {
                _autoSyncArmed = true;
                ConfigureLockSyncTimer();
            }

            string desktopDesc = effectiveMode == "static" ? S.DesktopModeStaticLabel : S.DesktopModeLiveLabel;
            NotifyUser(string.Format(S.ToastBothAppliedFormat, desktopDesc, w, h), showBalloonIfHidden: true);
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastBothFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task RestoreBothAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            bool hadLive = _desktopForm is { IsDisposed: false };
            bool hadDesktopBackup = SystemWallpaperService.HasBackup(_settings);
            bool hadLockBackup = LockScreenService.HasBackup(_settings);

            CloseDesktopForm();
            _settings.DesktopMode = "off";
            _settings.AutoSyncLockScreen = false;
            _autoSyncArmed = false;
            ConfigureLockSyncTimer();
            _settings.Save();

            bool desktopRestored = SystemWallpaperService.RestoreOriginalWallpaper(_settings);
            bool lockRestored = await LockScreenService.RestoreOriginalLockScreenAsync(_settings);

            if (desktopRestored && lockRestored)
            {
                NotifyUser(S.ToastRestoredBoth, showBalloonIfHidden: true);
            }
            else if (desktopRestored && !hadLockBackup)
            {
                NotifyUser(S.ToastRestoredOriginal, showBalloonIfHidden: true);
            }
            else if (!hadDesktopBackup && !hadLive && lockRestored)
            {
                NotifyUser(S.ToastRestoredLock, showBalloonIfHidden: true);
            }
            else
            {
                NotifyUser(S.ToastRestoredBothPartial, showBalloonIfHidden: true);
            }
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastRestoreFailedFormat, ex.Message), showBalloonIfHidden: true);
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task SaveScreenshotAsync()
    {
        if (!await TryEnterOperationGateAsync()) return;
        _isBusy = true;
        await PushStateToMainFormAsync();

        try
        {
            (byte[] bytes, string ext, int w, int h) = await CaptureCurrentSceneAsync();
            Localization.HostStrings s = S;
            string filePrefix = Localization.ResolveLanguage(_settings.LanguageMode) == "zh" ? "寸度" : "Slant";

            using SaveFileDialog dlg = new()
            {
                Title = s.SaveDialogTitle,
                Filter = ext == ".jpg" ? s.SaveDialogFilterJpg : s.SaveDialogFilterPng,
                FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dlg.ShowDialog(_mainForm) == DialogResult.OK)
            {
                File.WriteAllBytes(dlg.FileName, bytes);
                NotifyUser(string.Format(S.ToastScreenshotSavedFormat, w, h));
            }
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastScreenshotFailedFormat, ex.Message));
        }
        finally
        {
            _isBusy = false;
            ReleaseOperationGate();
            await PushStateToMainFormAsync();
        }
    }

    private async Task SetAutoSyncLockScreenAsync(bool enabled)
    {
        _settings.AutoSyncLockScreen = enabled;
        _autoSyncArmed = enabled;
        _settings.Save();
        ConfigureLockSyncTimer();
        await PushStateToMainFormAsync();
        NotifyUser(enabled
            ? string.Format(S.ToastAutoSyncOnFormat, _settings.SyncIntervalMinutes)
            : S.ToastAutoSyncOff);
    }

    private async Task SetSyncIntervalMinutesAsync(int minutes)
    {
        _settings.SyncIntervalMinutes = minutes is 15 or 30 or 60 ? minutes : 30;
        _settings.Save();
        ConfigureLockSyncTimer();
        await PushStateToMainFormAsync();
        NotifyUser(string.Format(S.ToastSyncIntervalFormat, _settings.SyncIntervalMinutes));
    }

    private async Task SetAutoStartAsync(bool enabled)
    {
        try
        {
            AutoStartService.SetEnabled(enabled);
            await PushStateToMainFormAsync();
            NotifyUser(enabled ? S.ToastAutoStartOn : S.ToastAutoStartOff);
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastAutoStartFailedFormat, ex.Message));
        }
    }

    private async Task SetPauseOnFullscreenAsync(bool enabled)
    {
        _settings.PauseOnFullscreenApp = enabled;
        _settings.Save();
        _fullscreenWatcher.SetEnabled(enabled);
        if (!enabled && _desktopForm is not null && !_desktopForm.IsDisposed)
        {
            await _desktopForm.SetHostSuspendedAsync(false);
        }
        await PushStateToMainFormAsync();
        NotifyUser(enabled ? S.ToastFullscreenPauseOn : S.ToastFullscreenPauseOff);
    }

    private void OpenWindowsLockScreenSettings()
    {
        try
        {
            LockScreenService.OpenWindowsLockScreenSettings();
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastOpenSettingsFailedFormat, ex.Message));
        }
    }

    private void OpenWindowsBackgroundSettings()
    {
        try
        {
            LockScreenService.OpenWindowsBackgroundSettings();
        }
        catch (Exception ex)
        {
            NotifyUser(string.Format(S.ToastOpenSettingsFailedFormat, ex.Message));
        }
    }

    private async void OnFullscreenStateChanged(bool isFullscreen)
    {
        if (_disposed) return;
        try
        {
            if (_settings.PauseOnFullscreenApp && _desktopForm is not null && !_desktopForm.IsDisposed)
            {
                await _desktopForm.SetHostSuspendedAsync(isFullscreen);
            }
            await PushStateToMainFormAsync();
        }
        catch
        {
            // Ignore transient fullscreen state transitions.
        }
    }

    private void OnExplorerRestarted()
    {
        if (_disposed) return;
        _trayIcon.Visible = true;
        if (_desktopForm is { IsDisposed: false } || (_launchedMinimized && _settings.DesktopMode == "live"))
        {
            EnsureDesktopFormRunning();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            RefreshTrayIconForTheme();
        }
    }

    private async void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        try
        {
            RefreshTrayIconForTheme();
            if (_settings.DesktopMode == "live" && _desktopForm is not null && !_desktopForm.IsDisposed)
            {
                _desktopForm.Bounds = SystemInformation.VirtualScreen;
                _desktopForm.ReEmbed();
            }
            await PushStateToMainFormAsync();
        }
        catch
        {
            // Ignore transient display change notifications.
        }
    }

    private async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_disposed || e.Mode != PowerModes.Resume) return;
        try
        {
            await HandleResumeOrUnlockAsync();
        }
        catch
        {
            // Ignore resume recovery failures.
        }
    }

    private async void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (_disposed || e.Reason != SessionSwitchReason.SessionUnlock) return;
        try
        {
            await HandleResumeOrUnlockAsync();
        }
        catch
        {
            // Ignore unlock recovery failures.
        }
    }

    private async Task HandleResumeOrUnlockAsync()
    {
        await Task.Delay(1200);
        if (_disposed) return;

        if (_desktopForm is { IsDisposed: false })
        {
            _desktopForm.ReEmbed();
        }

        if (_settings.AutoSyncLockScreen && _autoSyncArmed)
        {
            await ApplyLockScreenAsync(isAutoSync: true);
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        CleanupResources();
    }

    private void ExitApplication()
    {
        CleanupResources();
        ExitThread();
    }

    private void CleanupResources()
    {
        if (_disposed) return;
        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.SessionEnding -= OnSessionEnding;

        _lockSyncTimer.Stop();
        _lockSyncTimer.Dispose();

        _fullscreenWatcher.FullscreenStateChanged -= OnFullscreenStateChanged;
        _fullscreenWatcher.Dispose();

        CloseDesktopForm();

        if (!_trayFlyout.IsDisposed)
        {
            _trayFlyout.Close();
            _trayFlyout.Dispose();
        }

        if (_mainForm is not null && !_mainForm.IsDisposed)
        {
            _mainForm.CloseForExit();
            _mainForm.Dispose();
            _mainForm = null;
        }

        _messageWindow.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        _appIcon.Dispose();
        _operationGate.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupResources();
        }
        base.Dispose(disposing);
    }
}
