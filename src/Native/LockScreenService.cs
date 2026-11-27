using System.Diagnostics;
using Microsoft.Win32;
using ObliqueSeasons.Models;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ObliqueSeasons.Native;

/// <summary>
/// Captures/rotates lock screen images, backs up prior lock screen state, and applies/restores via WinRT LockScreen APIs.
/// </summary>
internal static class LockScreenService
{
    private const string SpotlightRegKey = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    public static bool HasBackup(AppSettings settings)
    {
        return (!string.IsNullOrEmpty(settings.OriginalLockScreenBackupPath) && File.Exists(settings.OriginalLockScreenBackupPath))
            || settings.OriginalLockScreenSpotlight == true;
    }

    public static async Task EnsureOriginalLockScreenBackedUpAsync(AppSettings settings)
    {
        if (HasBackup(settings))
        {
            return;
        }

        bool changed = false;
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SpotlightRegKey, false);
            if (key?.GetValue("RotatingLockScreenEnabled") is int intVal)
            {
                settings.OriginalLockScreenSpotlight = intVal != 0;
                changed = true;
            }
        }
        catch
        {
            // Spotlight state inspection is best-effort.
        }

        string? savedBackupPath = null;
        try
        {
            using IRandomAccessStream? stream = Windows.System.UserProfile.LockScreen.GetImageStream();
            if (stream is not null)
            {
                using Stream readStream = stream.AsStreamForRead();
                using MemoryStream ms = new();
                await readStream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();
                if (bytes.Length > 0)
                {
                    Directory.CreateDirectory(AppSettings.BackupsDirectory);
                    string ext = DetectImageExtension(bytes);
                    savedBackupPath = Path.Combine(AppSettings.BackupsDirectory, $"original_lockscreen{ext}");
                    await File.WriteAllBytesAsync(savedBackupPath, bytes);
                }
            }
        }
        catch
        {
            // Fall back to OriginalImageFile URI when stream access is unavailable.
        }

        if (savedBackupPath is null)
        {
            try
            {
                Uri? uri = Windows.System.UserProfile.LockScreen.OriginalImageFile;
                if (uri is { IsFile: true } && File.Exists(uri.LocalPath))
                {
                    string fullSrc = Path.GetFullPath(uri.LocalPath);
                    string wallpapersDir = Path.GetFullPath(AppSettings.WallpapersDirectory);
                    if (!fullSrc.StartsWith(wallpapersDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(AppSettings.BackupsDirectory);
                        string ext = Path.GetExtension(fullSrc);
                        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
                        savedBackupPath = Path.Combine(AppSettings.BackupsDirectory, $"original_lockscreen{ext}");
                        File.Copy(fullSrc, savedBackupPath, overwrite: true);
                    }
                }
            }
            catch
            {
                // OriginalImageFile lookup is best-effort.
            }
        }

        if (savedBackupPath is not null)
        {
            settings.OriginalLockScreenBackupPath = savedBackupPath;
            changed = true;
        }

        if (changed)
        {
            settings.Save();
        }
    }

    private static string DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ".png";
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return ".jpg";
        }
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return ".bmp";
        }
        return ".jpg";
    }

    public static async Task<string> ApplyLockScreenAsync(byte[] imageBytes, string extension, AppSettings settings)
    {
        await EnsureOriginalLockScreenBackedUpAsync(settings);

        string path = SystemWallpaperService.SaveRotatedImage("lockscreen", imageBytes, extension, keepCount: 2);

        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        await Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);

        settings.LastLockScreenSyncIso = DateTimeOffset.Now.ToString("O");
        settings.Save();
        return path;
    }

    public static async Task<bool> RestoreOriginalLockScreenAsync(AppSettings settings)
    {
        bool restored = false;

        if (!string.IsNullOrEmpty(settings.OriginalLockScreenBackupPath) &&
            File.Exists(settings.OriginalLockScreenBackupPath))
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(settings.OriginalLockScreenBackupPath);
            await Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);
            restored = true;
        }

        if (settings.OriginalLockScreenSpotlight == true)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SpotlightRegKey, true)
                    ?? Registry.CurrentUser.CreateSubKey(SpotlightRegKey, true);
                key?.SetValue("RotatingLockScreenEnabled", 1, RegistryValueKind.DWord);
                restored = true;
            }
            catch
            {
                // Best-effort Spotlight flag restoration.
            }
        }

        if (restored)
        {
            settings.LastLockScreenSyncIso = null;
            settings.OriginalLockScreenBackupPath = null;
            settings.OriginalLockScreenSpotlight = null;
            settings.AutoSyncLockScreen = false;
            settings.Save();
        }

        return restored;
    }

    public static void OpenWindowsLockScreenSettings()
    {
        LaunchSettingsUri("ms-settings:lockscreen");
    }

    public static void OpenWindowsBackgroundSettings()
    {
        LaunchSettingsUri("ms-settings:personalization-background");
    }

    private static void LaunchSettingsUri(string uri)
    {
        Process.Start(new ProcessStartInfo(uri)
        {
            UseShellExecute = true
        });
    }
}
