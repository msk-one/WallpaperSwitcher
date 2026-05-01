using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace WallpaperSwitcher;

public sealed class WallpaperService
{
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateIniFile = 0x0001;
    private const uint SendWinIniChange = 0x0002;

    public bool TryApply(string wallpaperPath, out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(wallpaperPath))
        {
            errorMessage = "The selected wallpaper file no longer exists.";
            return false;
        }

        if (!NativeMethods.SystemParametersInfo(
                SetDesktopWallpaper,
                0,
                wallpaperPath,
                UpdateIniFile | SendWinIniChange))
        {
            errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        return true;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
    }
}
