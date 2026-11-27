using System.Globalization;

namespace ObliqueSeasons.Services;

/// <summary>
/// Provides bilingual (Simplified Chinese / English) host strings resolved from system UI culture or user override.
/// </summary>
internal static class Localization
{
    internal sealed class HostStrings
    {
        public required string AppTitle { get; init; }
        public required string MenuOpenMain { get; init; }
        public required string MenuExit { get; init; }
        public required string NotSynced { get; init; }
        public required string NotApplied { get; init; }
        public required string BalloonStartedBackground { get; init; }
        public required string BalloonMinimized { get; init; }
        public required string MsgMissingHtml { get; init; }
        public required string MsgWebViewInitErrorFormat { get; init; }
        public required string StartupLoading { get; init; }
        public required string StartupFailedGeneric { get; init; }
        public required string StartupFailedRuntime { get; init; }
        public required string StartupFailedTimeout { get; init; }
        public required string RetryButton { get; init; }
        public required string ToastRefreshedEmbed { get; init; }
        public required string ToastDesktopOffInfo { get; init; }
        public required string ToastDesktopLiveSet { get; init; }
        public required string ToastDesktopOffSet { get; init; }
        public required string ToastStaticAppliedFormat { get; init; }
        public required string ToastStaticFailedFormat { get; init; }
        public required string ToastRestoredOriginal { get; init; }
        public required string ToastRestoreNotFound { get; init; }
        public required string ToastRestoreFailedFormat { get; init; }
        public required string ToastLockAppliedFormat { get; init; }
        public required string ToastLockFailedFormat { get; init; }
        public required string ToastRestoredLock { get; init; }
        public required string ToastRestoreLockNotFound { get; init; }
        public required string ToastRestoreLockFailedFormat { get; init; }
        public required string ToastBothAppliedFormat { get; init; }
        public required string ToastBothFailedFormat { get; init; }
        public required string ToastRestoredBoth { get; init; }
        public required string ToastRestoredBothPartial { get; init; }
        public required string DesktopModeStaticLabel { get; init; }
        public required string DesktopModeLiveLabel { get; init; }
        public required string SaveDialogTitle { get; init; }
        public required string SaveDialogFilterJpg { get; init; }
        public required string SaveDialogFilterPng { get; init; }
        public required string ToastScreenshotSavedFormat { get; init; }
        public required string ToastScreenshotFailedFormat { get; init; }
        public required string ToastAutoSyncOnFormat { get; init; }
        public required string ToastAutoSyncOff { get; init; }
        public required string ToastSyncIntervalFormat { get; init; }
        public required string ToastAutoStartOn { get; init; }
        public required string ToastAutoStartOff { get; init; }
        public required string ToastAutoStartFailedFormat { get; init; }
        public required string ToastFullscreenPauseOn { get; init; }
        public required string ToastFullscreenPauseOff { get; init; }
        public required string ToastMinimizeToTrayOn { get; init; }
        public required string ToastMinimizeToTrayOff { get; init; }
        public required string ToastOpenSettingsFailedFormat { get; init; }
        public required string ToastOperationFailedFormat { get; init; }
    }

    private static readonly HostStrings ZhStrings = new()
    {
        AppTitle = "寸度",
        MenuOpenMain = "系统设置",
        MenuExit = "退出",
        NotSynced = "尚未同步",
        NotApplied = "尚未应用",
        BalloonStartedBackground = "动态壁纸已在后台运行，双击托盘图标可打开参数面板或设置系统锁屏。",
        BalloonMinimized = "窗口已最小化至系统托盘，双击或右击托盘图标可打开系统设置。",
        MsgMissingHtml = "未找到壁纸核心文件：wallpaper\\index.html。\n请确认已完整解压后再运行程序。",
        MsgWebViewInitErrorFormat = "无法初始化 WebView2 渲染内核：\n{0}\n\n请确认系统已安装 Microsoft Edge WebView2 Runtime。",
        StartupLoading = "正在准备光影…",
        StartupFailedGeneric = "启动失败，未能加载光影画面。",
        StartupFailedRuntime = "启动失败，请确认已安装 WebView2 运行环境。",
        StartupFailedTimeout = "启动超时，请点击重试。",
        RetryButton = "重试",
        ToastRefreshedEmbed = "已重新适配桌面显示区域",
        ToastDesktopOffInfo = "当前桌面壁纸处于关闭状态",
        ToastDesktopLiveSet = "已设为 Windows 动态桌面壁纸",
        ToastDesktopOffSet = "已关闭动态桌面壁纸",
        ToastStaticAppliedFormat = "已应用静态桌面壁纸 ({0}×{1})",
        ToastStaticFailedFormat = "应用静态壁纸失败：{0}",
        ToastRestoredOriginal = "已恢复原有 Windows 桌面壁纸",
        ToastRestoreNotFound = "未找到可恢复的原壁纸文件，已关闭动态壁纸",
        ToastRestoreFailedFormat = "恢复原壁纸失败：{0}",
        ToastLockAppliedFormat = "已将当前画面设为系统锁屏 ({0}×{1})",
        ToastLockFailedFormat = "设置系统锁屏失败：{0}。若系统开启了「Windows 聚焦」，可在系统锁屏设置中切换为「图片」。",
        ToastRestoredLock = "已恢复原有 Windows 锁屏画面",
        ToastRestoreLockNotFound = "未找到可恢复的原锁屏备份",
        ToastRestoreLockFailedFormat = "恢复原锁屏失败：{0}",
        ToastBothAppliedFormat = "已同时应用到{0}与系统锁屏 ({1}×{2})",
        ToastBothFailedFormat = "同步桌面与锁屏失败：{0}",
        ToastRestoredBoth = "已恢复设置前的桌面壁纸与系统锁屏",
        ToastRestoredBothPartial = "已关闭动态壁纸并恢复可用备份",
        DesktopModeStaticLabel = "静态桌面",
        DesktopModeLiveLabel = "动态桌面",
        SaveDialogTitle = "保存寸度高清截图",
        SaveDialogFilterJpg = "JPEG 图像 (*.jpg)|*.jpg|所有文件 (*.*)|*.*",
        SaveDialogFilterPng = "PNG 图像 (*.png)|*.png|所有文件 (*.*)|*.*",
        ToastScreenshotSavedFormat = "已保存高清截图 ({0}×{1})",
        ToastScreenshotFailedFormat = "保存截图失败：{0}",
        ToastAutoSyncOnFormat = "已开启锁屏定时自动同步（每 {0} 分钟）",
        ToastAutoSyncOff = "已关闭锁屏定时自动同步",
        ToastSyncIntervalFormat = "锁屏自动同步间隔已设为每 {0} 分钟",
        ToastAutoStartOn = "已开启开机自动启动",
        ToastAutoStartOff = "已关闭开机自动启动",
        ToastAutoStartFailedFormat = "设置开机自启失败：{0}",
        ToastFullscreenPauseOn = "已开启全屏应用时暂停动态渲染",
        ToastFullscreenPauseOff = "已关闭全屏应用时暂停渲染",
        ToastMinimizeToTrayOn = "关闭窗口时将最小化到系统托盘",
        ToastMinimizeToTrayOff = "关闭窗口时将直接退出程序",
        ToastOpenSettingsFailedFormat = "无法打开系统设置：{0}",
        ToastOperationFailedFormat = "操作失败：{0}"
    };

    private static readonly HostStrings EnStrings = new()
    {
        AppTitle = "Slant",
        MenuOpenMain = "Settings",
        MenuExit = "Exit",
        NotSynced = "Not synced",
        NotApplied = "Not applied",
        BalloonStartedBackground = "Live wallpaper is running in the background. Double-click the tray icon to open settings.",
        BalloonMinimized = "Minimized to system tray. Right-click or double-click the tray icon to open Settings.",
        MsgMissingHtml = "Core wallpaper file not found: wallpaper\\index.html.\nPlease extract the full package before running.",
        MsgWebViewInitErrorFormat = "Failed to initialize WebView2 runtime:\n{0}\n\nPlease ensure Microsoft Edge WebView2 Runtime is installed.",
        StartupLoading = "Preparing window light…",
        StartupFailedGeneric = "Startup failed. Unable to load the scene.",
        StartupFailedRuntime = "Startup failed. Please ensure WebView2 Runtime is installed.",
        StartupFailedTimeout = "Startup timed out. Please try again.",
        RetryButton = "Retry",
        ToastRefreshedEmbed = "Re-fitted desktop display area",
        ToastDesktopOffInfo = "Desktop wallpaper is currently disabled",
        ToastDesktopLiveSet = "Set as Windows Live Desktop Wallpaper",
        ToastDesktopOffSet = "Live desktop wallpaper disabled",
        ToastStaticAppliedFormat = "Applied static desktop wallpaper ({0}×{1})",
        ToastStaticFailedFormat = "Failed to apply static wallpaper: {0}",
        ToastRestoredOriginal = "Restored original Windows desktop wallpaper",
        ToastRestoreNotFound = "Original wallpaper file not found; live wallpaper disabled",
        ToastRestoreFailedFormat = "Failed to restore original wallpaper: {0}",
        ToastLockAppliedFormat = "Set current scene as Windows Lock Screen ({0}×{1})",
        ToastLockFailedFormat = "Failed to set lock screen: {0}. If Windows Spotlight is active, switch to Picture in Lock Screen settings.",
        ToastRestoredLock = "Restored original Windows Lock Screen",
        ToastRestoreLockNotFound = "No backed-up original lock screen found",
        ToastRestoreLockFailedFormat = "Failed to restore original lock screen: {0}",
        ToastBothAppliedFormat = "Applied to {0} and Lock Screen ({1}×{2})",
        ToastBothFailedFormat = "Failed to sync desktop & lock screen: {0}",
        ToastRestoredBoth = "Restored pre-apply desktop wallpaper and lock screen",
        ToastRestoredBothPartial = "Disabled live wallpaper and restored available backups",
        DesktopModeStaticLabel = "Static Desktop",
        DesktopModeLiveLabel = "Live Desktop",
        SaveDialogTitle = "Save Slant High-Res Snapshot",
        SaveDialogFilterJpg = "JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
        SaveDialogFilterPng = "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
        ToastScreenshotSavedFormat = "Saved snapshot ({0}×{1})",
        ToastScreenshotFailedFormat = "Failed to save snapshot: {0}",
        ToastAutoSyncOnFormat = "Lock screen auto-sync enabled (every {0} min)",
        ToastAutoSyncOff = "Lock screen auto-sync disabled",
        ToastSyncIntervalFormat = "Lock screen sync interval set to every {0} min",
        ToastAutoStartOn = "Startup launch enabled",
        ToastAutoStartOff = "Startup launch disabled",
        ToastAutoStartFailedFormat = "Failed to update startup setting: {0}",
        ToastFullscreenPauseOn = "Pause on fullscreen apps enabled",
        ToastFullscreenPauseOff = "Pause on fullscreen apps disabled",
        ToastMinimizeToTrayOn = "Closing window will minimize to tray",
        ToastMinimizeToTrayOff = "Closing window will exit application",
        ToastOpenSettingsFailedFormat = "Unable to open Windows Settings: {0}",
        ToastOperationFailedFormat = "Operation failed: {0}"
    };

    public static string ResolveLanguage(string? languageMode)
    {
        if (string.Equals(languageMode, "zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh";
        }
        if (string.Equals(languageMode, "en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh"
            : "en";
    }

    public static HostStrings Get(string? languageMode)
    {
        return ResolveLanguage(languageMode) == "zh" ? ZhStrings : EnStrings;
    }
}
