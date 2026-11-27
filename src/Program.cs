using System.Text.Json;
using ObliqueSeasons.Models;
using ObliqueSeasons.Native;
using ObliqueSeasons.Services;

namespace ObliqueSeasons;

internal static class Program
{
    private const string SingleInstanceMutexName = "ObliqueSeasonsWindowsAppSingletonV2";

    [STAThread]
    private static int Main(string[] args)
    {
        if (HasArg(args, "--self-test"))
        {
            return RunSelfTest();
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogUnhandledException("UIThread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogUnhandledException("AppDomain", e.ExceptionObject as Exception);

        using Mutex mutex = new(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            if (WallpaperAppContext.WmShowMe != 0)
            {
                NativeMethods.PostMessage(
                    NativeMethods.HWND_BROADCAST,
                    WallpaperAppContext.WmShowMe,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            return 0;
        }

        string wallpaperHtml = Path.Combine(WebViewEnvironmentHost.WallpaperDirectory, "index.html");
        if (!File.Exists(wallpaperHtml))
        {
            Localization.HostStrings s = Localization.Get(AppSettings.Load().LanguageMode);
            MessageBox.Show(
                s.MsgMissingHtml,
                s.AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        bool startMinimized = HasArg(args, "--autostart")
            || HasArg(args, "--minimized")
            || HasArg(args, "--desktop");
        bool windowOnlyMode = HasArg(args, "--window");

        using WallpaperAppContext context = new(startMinimized, windowOnlyMode);
        Application.Run(context);
        return 0;
    }

    private static void LogUnhandledException(string source, Exception? ex)
    {
        if (ex is null) return;
        try
        {
            Directory.CreateDirectory(AppSettings.DataRootDirectory);
            string logPath = Path.Combine(AppSettings.DataRootDirectory, "crash.log");
            string entry = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] [{source}] {ex}{Environment.NewLine}{Environment.NewLine}";
            if (File.Exists(logPath) && new FileInfo(logPath).Length > 512 * 1024)
            {
                File.WriteAllText(logPath, entry);
            }
            else
            {
                File.AppendAllText(logPath, entry);
            }
        }
        catch
        {
            // Crash logging must never throw.
        }
    }

    private static bool HasArg(string[]? args, string flag)
    {
        if (args is null) return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static int RunSelfTest()
    {
        bool allPassed = true;

        void Report(string name, bool ok, string detail)
        {
            Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
            if (!ok) allPassed = false;
        }

        try
        {
            AppSettings defaults = new();
            AppSettings sample = new()
            {
                DesktopMode = "static",
                AutoSyncLockScreen = false,
                SyncIntervalMinutes = 15,
                PauseOnFullscreenApp = true,
                MinimizeToTrayOnClose = false,
                LanguageMode = "en",
                OriginalWallpaperPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
                OriginalWallpaperBackupCopyPath = @"C:\Temp\original_desktop.jpg",
                OriginalLockScreenBackupPath = @"C:\Temp\original_lockscreen.jpg",
                OriginalLockScreenSpotlight = true
            };
            string json = JsonSerializer.Serialize(sample);
            AppSettings? roundtrip = JsonSerializer.Deserialize<AppSettings>(json);
            bool settingsOk = defaults.DesktopMode == "off"
                && !defaults.AutoSyncLockScreen
                && roundtrip is not null
                && roundtrip.DesktopMode == "static"
                && !roundtrip.AutoSyncLockScreen
                && roundtrip.SyncIntervalMinutes == 15
                && roundtrip.LanguageMode == "en"
                && roundtrip.OriginalWallpaperPath == sample.OriginalWallpaperPath
                && roundtrip.OriginalWallpaperBackupCopyPath == sample.OriginalWallpaperBackupCopyPath
                && roundtrip.OriginalLockScreenBackupPath == sample.OriginalLockScreenBackupPath
                && roundtrip.OriginalLockScreenSpotlight == true
                && Localization.ResolveLanguage("en") == "en"
                && Localization.ResolveLanguage("zh") == "zh"
                && Localization.Get("zh").AppTitle == "寸度"
                && Localization.Get("en").AppTitle == "Slant";
            Report("AppSettingsAndLocalization", settingsOk, $"DefaultMode={defaults.DesktopMode}, Mode={roundtrip?.DesktopMode}, Lang={roundtrip?.LanguageMode}");
        }
        catch (Exception ex)
        {
            Report("AppSettingsRoundtrip", false, ex.Message);
        }

        try
        {
            (int w, int h) = NativeMethods.GetPrimaryMonitorPhysicalResolution();
            Report("PrimaryMonitorResolution", w > 0 && h > 0, $"{w}x{h}");
        }
        catch (Exception ex)
        {
            Report("PrimaryMonitorResolution", false, ex.Message);
        }

        try
        {
            Type lockScreenType = typeof(Windows.System.UserProfile.LockScreen);
            Type storageFileType = typeof(Windows.Storage.StorageFile);
            Type webView2Type = typeof(Microsoft.Web.WebView2.WinForms.WebView2);
            var setMethod = lockScreenType.GetMethod("SetImageFileAsync");
            var getStreamMethod = lockScreenType.GetMethod("GetImageStream");
            var getFileMethod = storageFileType.GetMethod("GetFileFromPathAsync", new[] { typeof(string) });
            bool winrtOk = setMethod is not null && getStreamMethod is not null && getFileMethod is not null;
            Report("WinRTAndWebView2Load", winrtOk,
                $"{lockScreenType.FullName}, {storageFileType.FullName}, {webView2Type.FullName}");
        }
        catch (Exception ex)
        {
            Report("WinRTAndWebView2Load", false, ex.Message);
        }

        try
        {
            string htmlPath = Path.Combine(WebViewEnvironmentHost.WallpaperDirectory, "index.html");
            bool exists = File.Exists(htmlPath);
            string html = exists ? File.ReadAllText(htmlPath) : string.Empty;
            bool hasBridge = exists
                && html.Contains("obliqueHostCaptureSnapshot", StringComparison.Ordinal)
                && html.Contains("obliqueHostUpdateState", StringComparison.Ordinal)
                && html.Contains("obliqueHostSetPaused", StringComparison.Ordinal)
                && html.Contains("isHostMode", StringComparison.Ordinal)
                && html.Contains("applyTranslations", StringComparison.Ordinal)
                && html.Contains("weatherMode", StringComparison.Ordinal)
                && html.Contains("manualTuning", StringComparison.Ordinal)
                && html.Contains("btn-apply-desktop", StringComparison.Ordinal)
                && html.Contains("btn-apply-lock", StringComparison.Ordinal);
            Report("WallpaperHtmlAndBridge", hasBridge, exists ? $"Path={htmlPath}, Length={html.Length}" : $"Missing {htmlPath}");
        }
        catch (Exception ex)
        {
            Report("WallpaperHtmlAndBridge", false, ex.Message);
        }

        try
        {
            string version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            Report("WebView2LoaderRuntime", !string.IsNullOrWhiteSpace(version), $"WebView2Version={version}");
        }
        catch (Exception ex)
        {
            Report("WebView2LoaderRuntime", false, ex.Message);
        }

        return allPassed ? 0 : 1;
    }
}
