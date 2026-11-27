using System.Text.Json;

namespace ObliqueSeasons.Models;

/// <summary>
/// Persisted user preferences and wallpaper backup metadata stored under %LocalAppData%\ObliqueSeasons\settings.json.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DesktopMode { get; set; } = "off";
    public bool AutoSyncLockScreen { get; set; } = false;
    public int SyncIntervalMinutes { get; set; } = 30;
    public bool PauseOnFullscreenApp { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public string LanguageMode { get; set; } = "auto";
    public string? OriginalWallpaperPath { get; set; }
    public string? OriginalWallpaperBackupCopyPath { get; set; }
    public string? OriginalWallpaperStyle { get; set; }
    public string? OriginalTileWallpaper { get; set; }
    public string? OriginalLockScreenBackupPath { get; set; }
    public bool? OriginalLockScreenSpotlight { get; set; }
    public string? LastLockScreenSyncIso { get; set; }
    public string? LastDesktopSyncIso { get; set; }

    public static string DataRootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ObliqueSeasons");

    public static string WallpapersDirectory => Path.Combine(DataRootDirectory, "Wallpapers");

    public static string BackupsDirectory => Path.Combine(DataRootDirectory, "Backups");

    public static string SettingsFilePath => Path.Combine(DataRootDirectory, "settings.json");

    /// <summary>
    /// Ensures all settings stay within valid discrete values.
    /// </summary>
    public void Normalize()
    {
        if (DesktopMode is not ("live" or "static" or "off"))
        {
            DesktopMode = "off";
        }

        if (SyncIntervalMinutes is not (15 or 30 or 60))
        {
            SyncIntervalMinutes = 30;
        }

        if (LanguageMode is not ("auto" or "zh" or "en"))
        {
            LanguageMode = "auto";
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults if settings file is corrupted or inaccessible.
        }

        AppSettings defaults = new();
        defaults.Normalize();
        return defaults;
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(DataRootDirectory);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        string tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DesktopMode = DesktopMode,
            AutoSyncLockScreen = AutoSyncLockScreen,
            SyncIntervalMinutes = SyncIntervalMinutes,
            PauseOnFullscreenApp = PauseOnFullscreenApp,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            LanguageMode = LanguageMode,
            OriginalWallpaperPath = OriginalWallpaperPath,
            OriginalWallpaperBackupCopyPath = OriginalWallpaperBackupCopyPath,
            OriginalWallpaperStyle = OriginalWallpaperStyle,
            OriginalTileWallpaper = OriginalTileWallpaper,
            OriginalLockScreenBackupPath = OriginalLockScreenBackupPath,
            OriginalLockScreenSpotlight = OriginalLockScreenSpotlight,
            LastLockScreenSyncIso = LastLockScreenSyncIso,
            LastDesktopSyncIso = LastDesktopSyncIso
        };
    }
}
