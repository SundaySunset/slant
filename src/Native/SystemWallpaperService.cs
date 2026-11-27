using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using ObliqueSeasons.Models;

namespace ObliqueSeasons.Native;

/// <summary>
/// Manages static Windows desktop wallpaper backup, application, and restoration.
/// </summary>
internal static class SystemWallpaperService
{
    private const string DesktopRegKey = @"Control Panel\Desktop";

    public static string GetCurrentWallpaperPath()
    {
        StringBuilder sb = new(1024);
        if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
        {
            return sb.ToString().TrimEnd('\0');
        }
        return string.Empty;
    }

    public static bool HasBackup(AppSettings settings)
    {
        return settings.OriginalWallpaperPath is not null
            || (!string.IsNullOrEmpty(settings.OriginalWallpaperBackupCopyPath)
                && File.Exists(settings.OriginalWallpaperBackupCopyPath));
    }

    public static void EnsureOriginalWallpaperBackedUp(AppSettings settings)
    {
        if (HasBackup(settings))
        {
            return;
        }

        string currentPath = GetCurrentWallpaperPath();
        string wallpapersDir = Path.GetFullPath(AppSettings.WallpapersDirectory);
        if (!string.IsNullOrEmpty(currentPath))
        {
            string fullCurrent = Path.GetFullPath(currentPath);
            if (fullCurrent.StartsWith(wallpapersDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        settings.OriginalWallpaperPath = currentPath;
        if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
        {
            try
            {
                Directory.CreateDirectory(AppSettings.BackupsDirectory);
                string ext = Path.GetExtension(currentPath);
                if (string.IsNullOrWhiteSpace(ext))
                {
                    ext = ".jpg";
                }
                string backupCopy = Path.Combine(AppSettings.BackupsDirectory, $"original_desktop{ext}");
                File.Copy(currentPath, backupCopy, overwrite: true);
                settings.OriginalWallpaperBackupCopyPath = backupCopy;
            }
            catch
            {
                // Best-effort copy backup; OriginalWallpaperPath remains recorded.
            }
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegKey, false);
            settings.OriginalWallpaperStyle = key?.GetValue("WallpaperStyle")?.ToString();
            settings.OriginalTileWallpaper = key?.GetValue("TileWallpaper")?.ToString();
        }
        catch
        {
            // Registry metadata backup is best-effort.
        }
        settings.Save();
    }

    public static string ApplyStaticWallpaper(byte[] imageBytes, string extension, AppSettings settings)
    {
        EnsureOriginalWallpaperBackedUp(settings);

        string path = SaveRotatedImage("desktop", imageBytes, extension, keepCount: 2);

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegKey, true);
            if (key is not null)
            {
                key.SetValue("WallpaperStyle", "10", RegistryValueKind.String);
                key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
            }
        }
        catch
        {
            // Continue even if style registry write is blocked by policy.
        }

        bool ok = NativeMethods.SystemParametersInfo(
            NativeMethods.SPI_SETDESKWALLPAPER,
            0,
            path,
            NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);

        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Win32Exception(err, $"设置系统静态壁纸失败 (Win32 error {err})。");
        }

        settings.LastDesktopSyncIso = DateTimeOffset.Now.ToString("O");
        settings.Save();
        return path;
    }

    public static bool RestoreOriginalWallpaper(AppSettings settings)
    {
        string? targetPath = null;
        string? original = settings.OriginalWallpaperPath;

        if (!string.IsNullOrEmpty(original) &&
            File.Exists(original) &&
            !string.Equals(Path.GetFileName(original), "TranscodedWallpaper", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = original;
        }
        else if (!string.IsNullOrEmpty(settings.OriginalWallpaperBackupCopyPath) &&
                 File.Exists(settings.OriginalWallpaperBackupCopyPath))
        {
            targetPath = settings.OriginalWallpaperBackupCopyPath;
        }
        else if (original == string.Empty)
        {
            targetPath = string.Empty;
        }

        if (targetPath is null)
        {
            return false;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegKey, true);
            if (key is not null)
            {
                if (!string.IsNullOrEmpty(settings.OriginalWallpaperStyle))
                {
                    key.SetValue("WallpaperStyle", settings.OriginalWallpaperStyle, RegistryValueKind.String);
                }
                if (!string.IsNullOrEmpty(settings.OriginalTileWallpaper))
                {
                    key.SetValue("TileWallpaper", settings.OriginalTileWallpaper, RegistryValueKind.String);
                }
            }
        }
        catch
        {
            // Best-effort style restore.
        }

        bool ok = NativeMethods.SystemParametersInfo(
            NativeMethods.SPI_SETDESKWALLPAPER,
            0,
            targetPath,
            NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);

        if (ok)
        {
            settings.LastDesktopSyncIso = null;
            settings.OriginalWallpaperPath = null;
            settings.OriginalWallpaperBackupCopyPath = null;
            settings.OriginalWallpaperStyle = null;
            settings.OriginalTileWallpaper = null;
            settings.Save();
        }

        return ok;
    }

    /// <summary>
    /// Writes a uniquely named timestamped image so Windows refreshes its wallpaper/lockscreen cache,
    /// while pruning older files beyond <paramref name="keepCount"/>.
    /// </summary>
    internal static string SaveRotatedImage(string prefix, byte[] imageBytes, string extension, int keepCount = 2)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("图像数据为空。", nameof(imageBytes));
        }

        string ext = string.IsNullOrWhiteSpace(extension) ? ".png" : extension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }
        if (ext is not (".png" or ".jpg" or ".jpeg"))
        {
            ext = ".png";
        }

        string dir = AppSettings.WallpapersDirectory;
        Directory.CreateDirectory(dir);

        string fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}";
        string fullPath = Path.Combine(dir, fileName);
        File.WriteAllBytes(fullPath, imageBytes);

        try
        {
            List<FileInfo> existing = new DirectoryInfo(dir)
                .GetFiles($"{prefix}_*.*")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .ToList();

            for (int i = keepCount; i < existing.Count; i++)
            {
                try
                {
                    existing[i].Delete();
                }
                catch
                {
                    // File may still be locked briefly by Windows shell.
                }
            }
        }
        catch
        {
            // Pruning failure is non-fatal.
        }

        return fullPath;
    }
}
