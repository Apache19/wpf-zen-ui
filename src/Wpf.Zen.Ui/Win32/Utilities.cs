// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wpf.Zen.Ui.Win32;

/// <summary>
/// Common Window utilities.
/// </summary>
// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Utilities
{
    private static readonly PlatformID _osPlatform = Environment.OSVersion.Platform;

    private static readonly Version Vista    = new(6, 0);
    private static readonly Version Windows7 = new(6, 1);
    private static readonly Version Windows8 = new(6, 2);

    #if NET5_0_OR_GREATER
    private static readonly Version _osVersion = Environment.OSVersion.Version;
    #else
    private static readonly Version _osVersion = GetOSVersionFromRegistry();
    #endif

    /// <summary>
    /// Gets a value indicating whether the operating system is NT or newer.
    /// </summary>
    public static bool IsNT => _osPlatform == PlatformID.Win32NT;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 6.0.
    /// </summary>
    public static bool IsVista_Or_Greater => _osVersion >= Vista;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 6.1.
    /// </summary>
    public static bool IsWin_7_Or_Greater => _osVersion >= Windows7;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 6.2.
    /// </summary>
    public static bool IsWin_8_Or_Greater => _osVersion >= Windows8;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 10.0* (build 10240).
    /// </summary>
    public static bool IsWin_10_Or_Greater => _osVersion.Build >= 10240;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 10.0* (build 14393).
    /// </summary>
    public static bool IsWin_10_1607_Or_Greater => _osVersion.Build >= 14393;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 10.0* (build 22000).
    /// </summary>
    public static bool IsWin_11_Or_Greater => _osVersion.Build >= 22000;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 10.0* (build 22523).
    /// </summary>
    public static bool IsWin_11_Insider1_Or_Greater => _osVersion.Build >= 22523;

    /// <summary>
    /// Gets a value indicating whether the operating system version is greater than or equal to 10.0* (build 22557).
    /// </summary>
    public static bool IsWin_11_Insider2_Or_Greater => _osVersion.Build >= 22557;

    // 这是正式支持 SYSTEMBACKDROP 的版本
    public static bool IsWin_11_22H2_Or_Greater => _osVersion.Build >= 22621;

    /// <summary>
    /// Get the WindowState as the native HWND knows it to be.
    /// This isn't necessarily the same as what Window thinks.
    /// </summary>
    public static WindowState GetHwndState(HWND hwnd)
    {
        WINDOWPLACEMENT wpl = new() { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };

        if (PInvoke.GetWindowPlacement(hwnd, ref wpl))
        {
            return wpl.showCmd switch
            {
                SHOW_WINDOW_CMD.SW_MINIMIZE => WindowState.Minimized,
                SHOW_WINDOW_CMD.SW_MAXIMIZE => WindowState.Maximized,
                _                           => WindowState.Normal
            };
        }

        throw new Win32Exception();
    }

    /// <summary>
    /// Gets a value indicating whether Desktop Window Manager (DWM) composition is enabled.
    /// </summary>
    public static bool IsCompositionEnabled
    {
        get
        {
            if (!IsVista_Or_Greater)
            {
                return false;
            }

            return PInvoke.DwmIsCompositionEnabled(out BOOL enabled) == HRESULT.S_OK & enabled;
        }
    }

    /// <summary>
    /// Tries to get the currently active window from the foreground HWND.
    /// </summary>
    /// <returns>The active <see cref="Window"/> or <see langword="null"/> if it cannot be determined.</returns>
    internal static Window? TryGetWindowFromForegroundHwnd()
    {
        try
        {
            HWND hwnd = PInvoke.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            var src = HwndSource.FromHwnd(hwnd);
            return src?.RootVisual switch
            {
                Window w            => w,
                DependencyObject rd => Window.GetWindow(rd),
                _                   => null,
            };
        }
        catch
        {
            return null;
        }
    }

    #if !NET5_0_OR_GREATER
    /// <summary>
    /// Tries to get the OS version from the Windows registry.
    /// </summary>
    private static Version GetOSVersionFromRegistry()
    {
        int major = 0;
        {
            // The 'CurrentMajorVersionNumber' string value in the CurrentVersion key is new for Windows 10,
            // and will most likely (hopefully) be there for some time before MS decides to change this - again...
            if (
                TryGetRegistryKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentMajorVersionNumber",
                    out var majorObj
                )
            )
            {
                majorObj ??= 0;

                major = (int)majorObj;
            }
            else // When the 'CurrentMajorVersionNumber' value is not present we fallback to reading the previous key used for this: 'CurrentVersion'
            if (
                TryGetRegistryKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentVersion",
                    out var version
                )
            )
            {
                version ??= string.Empty;

                var versionParts = ((string)version).Split('.');

                if (versionParts.Length >= 2)
                {
                    major = int.TryParse(versionParts[0], out int majorAsInt) ? majorAsInt : 0;
                }
            }
        }

        int minor = 0;
        {
            // The 'CurrentMinorVersionNumber' string value in the CurrentVersion key is new for Windows 10,
            // and will most likely (hopefully) be there for some time before MS decides to change this - again...
            if (
                TryGetRegistryKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentMinorVersionNumber",
                    out var minorObj
                )
            )
            {
                minorObj ??= string.Empty;

                minor = (int)minorObj;
            }
            else // When the 'CurrentMinorVersionNumber' value is not present we fallback to reading the previous key used for this: 'CurrentVersion'
            if (
                TryGetRegistryKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentVersion",
                    out var version
                )
            )
            {
                version ??= string.Empty;

                var versionParts = ((string)version).Split('.');

                if (versionParts.Length >= 2)
                {
                    minor = int.TryParse(versionParts[1], out int minorAsInt) ? minorAsInt : 0;
                }
            }
        }

        int build = 0;
        {
            if (
                TryGetRegistryKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentBuildNumber",
                    out var buildObj
                )
            )
            {
                buildObj ??= string.Empty;

                build = int.TryParse((string)buildObj, out int buildAsInt) ? buildAsInt : 0;
            }
        }

        return new(major, minor, build);
    }

    private static bool TryGetRegistryKey(string path, string key, out object? value)
    {
        value = null;

        try
        {
            using Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);

            if (rk == null)
            {
                return false;
            }

            value = rk.GetValue(key);

            return value != null;
        }
        catch
        {
            return false;
        }
    }

    #endif
}
