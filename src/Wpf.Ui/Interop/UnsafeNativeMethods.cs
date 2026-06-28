// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* This Source Code is partially based on reverse engineering of the Windows Operating System,
   and is intended for use on Windows systems only.
   This Source Code is partially based on the source code provided by the .NET Foundation. */

using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Interop;

/// <summary>
/// A set of dangerous methods to modify the appearance.
/// </summary>
internal static class UnsafeNativeMethods
{
    /// <summary>
    /// Tries to set the corner preference of the selected window.
    /// </summary>
    /// <param name="handle">Selected window handle.</param>
    /// <param name="cornerPreference">Window corner preference.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static unsafe bool ApplyWindowCornerPreference(
        IntPtr handle,
        WindowCornerPreference cornerPreference
    )
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(handle)))
        {
            return false;
        }

        DWM_WINDOW_CORNER_PREFERENCE pvAttribute = UnsafeReflection.Cast(cornerPreference);

        return PInvoke.DwmSetWindowAttribute(
                new HWND(handle),
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &pvAttribute,
                sizeof(int)
            ) == HRESULT.S_OK;
    }

    /// <summary>
    /// Tries to apply the color of the border.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true" /> if invocation of native Windows function succeeds.</returns>
    public static bool ApplyBorderColor(Window window, Color color) =>
        GetHandle(window, out IntPtr windowHandle) && ApplyBorderColor(windowHandle, color);

    /// <summary>
    /// Tries to apply the color of the border.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true" /> if invocation of native Windows function succeeds.</returns>
    public static bool ApplyBorderColor(Window window, int color) =>
        GetHandle(window, out IntPtr windowHandle) && ApplyBorderColor(windowHandle, color);

    /// <summary>
    /// Tries to apply the color of the border.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static bool ApplyBorderColor(IntPtr handle, Color color)
    {
        return ApplyBorderColor(handle, (color.B << 16) | (color.G << 8) | color.R);
    }

    /// <summary>
    /// Tries to apply the color of the border.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static unsafe bool ApplyBorderColor(IntPtr handle, int color)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(handle)))
        {
            return false;
        }

        return PInvoke.DwmSetWindowAttribute(
                new HWND(handle),
                DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR,
                &color,
                sizeof(int)
            ) == HRESULT.S_OK;
    }

    /// <summary>
    /// Checks whether accent color on title bars and window borders is enabled in Windows settings.
    /// </summary>
    /// <returns><see langword="true"/> if accent color on title bars is enabled.</returns>
    public static bool IsAccentColorOnTitleBarsEnabled()
    {
        RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        try
        {
            if (key?.GetValue("ColorPrevalence") is int value)
            {
                return value == 1;
            }
        }
        catch
        {
            // Ignore registry access errors
        }
        finally
        {
            key?.Dispose();
        }

        return false;
    }

    /// <summary>
    /// Tries to remove ImmersiveDarkMode effect from the <see cref="Window"/>.
    /// </summary>
    /// <param name="window">The window to which the effect is to be applied.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static bool RemoveWindowDarkMode(Window? window) =>
        GetHandle(window, out IntPtr windowHandle) && RemoveWindowDarkMode(windowHandle);

    /// <summary>
    /// Tries to remove ImmersiveDarkMode effect from the window handle.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static unsafe bool RemoveWindowDarkMode(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(handle)))
        {
            return false;
        }

        BOOL pvAttribute = false;
        DWMWINDOWATTRIBUTE dwAttribute = DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE;

        if (!Win32.Utilities.IsWin_11_Insider1_Or_Greater)
        {
            dwAttribute = DWMWINDOWATTRIBUTE.DMWA_USE_IMMERSIVE_DARK_MODE_OLD;
        }

        return PInvoke.DwmSetWindowAttribute(new HWND(handle), dwAttribute, &pvAttribute, (uint)sizeof(BOOL))
            == HRESULT.S_OK;
    }

    /// <summary>
    /// Tries to apply ImmersiveDarkMode effect for the <see cref="Window"/>.
    /// </summary>
    /// <param name="window">The window to which the effect is to be applied.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static bool ApplyWindowDarkMode(Window? window) =>
        GetHandle(window, out IntPtr windowHandle) && ApplyWindowDarkMode(windowHandle);

    /// <summary>
    /// Tries to apply ImmersiveDarkMode effect for the window handle.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static unsafe bool ApplyWindowDarkMode(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(handle)))
        {
            return false;
        }

        BOOL pvAttribute = true;
        DWMWINDOWATTRIBUTE dwAttribute = DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE;

        if (!Win32.Utilities.IsWin_11_Insider1_Or_Greater)
        {
            dwAttribute = DWMWINDOWATTRIBUTE.DMWA_USE_IMMERSIVE_DARK_MODE_OLD;
        }

        return PInvoke.DwmSetWindowAttribute(new HWND(handle), dwAttribute, &pvAttribute, (uint)sizeof(BOOL))
            == HRESULT.S_OK;
    }

    /// <summary>
    /// Tries to remove titlebar from selected <see cref="Window"/>.
    /// </summary>
    /// <param name="window">The window to which the effect is to be applied.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static bool RemoveWindowTitlebarContents(Window? window)
    {
        if (window == null)
        {
            return false;
        }

        if (window.IsLoaded)
        {
            return GetHandle(window, out IntPtr windowHandle) && RemoveWindowTitlebarContents(windowHandle);
        }

        window.Loaded += (sender, _1) =>
        {
            _ = GetHandle(sender as Window, out IntPtr windowHandle);
            _ = RemoveWindowTitlebarContents(windowHandle);
        };

        return true;
    }

    /// <summary>
    /// Tries to remove titlebar from selected window handle.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <returns><see langword="true"/> if invocation of native Windows function succeeds.</returns>
    public static bool RemoveWindowTitlebarContents(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var hwnd = new HWND(handle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return false;
        }

        const int SysMenuFlag = (int)WINDOW_STYLE.WS_SYSMENU;

        var style   = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var updated = style & ~SysMenuFlag;

        if (updated != style)
        {
            _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, updated);
        }

        return (PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE) & SysMenuFlag) == 0;
    }

    /// <summary>
    /// Tries to get currently selected Window accent color.
    /// </summary>
    public static Color GetAccentColor()
    {
        try
        {
            var accentColorValue = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                "AccentColor",
                null
            );

            if (accentColorValue is not null)
            {
                var accentColor = (uint)(int)accentColorValue;
                var values = BitConverter.GetBytes(accentColor);

                return Color.FromArgb(255, values[0], values[1], values[2]);
            }
        }
        catch
        {
            // Ignored.
        }

        // Windows default accent color
        // https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-shell-setup-themes-windowcolor#values
        return Color.FromArgb(0xff, 0x00, 0x78, 0xd7);
    }

    /// <summary>
    /// Tries to set taskbar state for the selected window handle.
    /// </summary>
    /// <param name="hWnd">Window handle.</param>
    /// <param name="taskbarFlag">Taskbar flag.</param>
    internal static bool SetTaskbarState(IntPtr hWnd, TBPFLAG taskbarFlag)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(hWnd)))
        {
            return false;
        }

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (new TaskbarList() is not ITaskbarList4 taskbarList)
        {
            return false;
        }

        taskbarList.HrInit();
        taskbarList.SetProgressState(new HWND(hWnd), taskbarFlag);

        return true;
    }

    /// <summary>
    /// Updates the taskbar progress bar value for a window.
    /// </summary>
    /// <param name="hWnd">The handle to the window.</param>
    /// <param name="taskbarFlag">Progress state flag (paused, etc).</param>
    /// <param name="current">Current progress value.</param>
    /// <param name="total">Maximum progress value.</param>
    /// <returns>True if successful updated, otherwise false.</returns>
    internal static bool SetTaskbarValue(IntPtr hWnd, TBPFLAG taskbarFlag, int current, int total)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(hWnd)))
        {
            return false;
        }

        /* TODO: Get existing taskbar class */

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (new TaskbarList() is not ITaskbarList4 taskbarList)
        {
            return false;
        }

        taskbarList.HrInit();
        taskbarList.SetProgressState(new HWND(hWnd), taskbarFlag);

        if (taskbarFlag is not TBPFLAG.TBPF_INDETERMINATE and not TBPFLAG.TBPF_NOPROGRESS)
        {
            taskbarList.SetProgressValue(new HWND(hWnd), Convert.ToUInt64(current), Convert.ToUInt64(total));
        }

        return true;
    }

    internal static Rect GetMonitorBounds(Rect bounds)
    {
        var nativeBounds = new RECT
        {
            left   = (int)Math.Floor(bounds.Left),
            top    = (int)Math.Floor(bounds.Top),
            right  = (int)Math.Ceiling(bounds.Right),
            bottom = (int)Math.Ceiling(bounds.Bottom),
        };

        var monitor = PInvoke.MonitorFromRect(in nativeBounds, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>(),
        };

        if (PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
        {
            var monitorBounds = monitorInfo.rcMonitor;
            return new Rect(monitorBounds.left, monitorBounds.top, monitorBounds.Width, monitorBounds.Height);
        }

        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );
    }

    public static unsafe bool RemoveWindowCaption(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(hWnd)))
        {
            return false;
        }

        var wtaOptions = new WTA_OPTIONS()
        {
            dwFlags = PInvoke.WTNCA_NODRAWCAPTION
                    | PInvoke.WTNCA_NODRAWICON
                    | PInvoke.WTNCA_NOMIRRORHELP
                    | PInvoke.WTNCA_NOSYSMENU,
            dwMask = PInvoke.WTNCA_NODRAWCAPTION
                   | PInvoke.WTNCA_NODRAWICON
                   | PInvoke.WTNCA_NOMIRRORHELP
                   | PInvoke.WTNCA_NOSYSMENU,
        };

        return PInvoke.SetWindowThemeAttribute(
                new HWND(hWnd),
                WINDOWTHEMEATTRIBUTETYPE.WTA_NONCLIENT,
                &wtaOptions,
                (uint)sizeof(WTA_OPTIONS)
            ) == HRESULT.S_OK;
    }

    /// <summary>
    /// Tries to get the pointer to the window handle.
    /// </summary>
    /// <returns><see langword="true"/> if the handle is not <see cref="IntPtr.Zero"/>.</returns>
    private static bool GetHandle(Window? window, out IntPtr windowHandle)
    {
        if (window is null)
        {
            windowHandle = IntPtr.Zero;

            return false;
        }

        windowHandle = new WindowInteropHelper(window).Handle;

        return windowHandle != IntPtr.Zero;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DWM_COLORIZATION_PARAMS
    {
        public COLORREF Color;
        public COLORREF Afterglow;
        public uint     Intensity;
        public uint     AfterGlowBalance;
        public uint     BlurBalance;
        public uint     GlassReflectionIntensity;
        public bool     Opaque;
    }

    [DllImport("dwmapi.dll", EntryPoint = "#127", PreserveSig = false)]
    internal static extern void DwmGetColorizationParameters(out DWM_COLORIZATION_PARAMS parameters);
}
