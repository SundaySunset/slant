using Microsoft.Win32;

namespace ObliqueSeasons.Services;

/// <summary>
/// Manages per-user Windows logon autostart in HKCU Run without elevation.
/// </summary>
internal static class AutoStartService
{
    private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ObliqueSeasonsWallpaper";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegPath, false);
            string? val = key?.GetValue(ValueName)?.ToString();
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunRegPath, true);
        if (key is null)
        {
            throw new InvalidOperationException("无法打开当前用户开机自启注册表项。");
        }

        if (enabled)
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue(ValueName, $"\"{exePath}\" --autostart", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
