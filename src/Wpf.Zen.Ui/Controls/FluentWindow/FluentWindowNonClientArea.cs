// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains the deterministic geometry used by FluentWindow's native message handler.
/// </summary>
internal static class FluentWindowNonClientArea
{
    private static readonly IntPtr[,] HitTestBorders =
    {
        { (IntPtr)PInvoke.HTTOPLEFT,    (IntPtr)PInvoke.HTTOP,    (IntPtr)PInvoke.HTTOPRIGHT    },
        { (IntPtr)PInvoke.HTLEFT,       IntPtr.Zero,              (IntPtr)PInvoke.HTRIGHT       },
        { (IntPtr)PInvoke.HTBOTTOMLEFT, (IntPtr)PInvoke.HTBOTTOM, (IntPtr)PInvoke.HTBOTTOMRIGHT },
    };

    /// <summary>
    /// Measures the resize frame that DefWindowProc selected for the proposed window rectangle.
    /// </summary>
    /// <remarks>
    /// The native client's top offset includes the caption, so it cannot represent the top resize frame.
    /// Standard overlapped windows use the same vertical frame thickness at the top and bottom; consequently
    /// the bottom offset is the only caption-independent source for the top resize thickness.
    /// </remarks>
    internal static WindowFrameInsets MeasureFrame(in RECT windowRect, in RECT nativeClientRect)
    {
        var left          = nativeClientRect.left - windowRect.left;
        var right         = windowRect.right  - nativeClientRect.right;
        var verticalFrame = windowRect.bottom - nativeClientRect.bottom;

        return new WindowFrameInsets(left, verticalFrame, right, verticalFrame);
    }

    /// <summary>
    /// Selects the frame policy without coupling geometry tests to the process operating system.
    /// </summary>
    /// <remarks>
    /// Windows 10 and Windows 11 both expose a native-border opt-out. Their opt-out visuals differ: Windows 11
    /// retains a one-physical-pixel DWM frame extension for its outline and rounded corners, whereas Windows 10
    /// needs the same WPF border used by the older-system fallback.
    /// </remarks>
    internal static FluentWindowFrameStrategy SelectFrameStrategy(
        bool isWindows11OrGreater,
        bool isWindows10OrGreater,
        bool useNativeWindowBorder
    )
    {
        if (isWindows11OrGreater)
        {
            return useNativeWindowBorder
                ? FluentWindowFrameStrategy.Windows11Native
                : FluentWindowFrameStrategy.Windows11Client;
        }

        if (isWindows10OrGreater && useNativeWindowBorder)
        {
            return FluentWindowFrameStrategy.Windows10Hybrid;
        }

        return FluentWindowFrameStrategy.ClientBorder;
    }

    /// <summary>
    /// Calculates the client rectangle returned to Windows for the selected frame strategy.
    /// </summary>
    internal static RECT CalculateClientRect(
        in RECT windowRect,
        in WindowFrameInsets frame,
        FluentWindowFrameStrategy strategy,
        bool isMaximized
    )
    {
        if (isMaximized)
        {
            return Inset(windowRect, frame.Left, frame.Top, frame.Right, frame.Bottom);
        }

        if (
            strategy is FluentWindowFrameStrategy.Windows11Native or FluentWindowFrameStrategy.Windows10Hybrid
        )
        {
            // Windows 11 no longer produces the false caption seen on older DWM versions, but reserving its whole
            // top resize frame still creates a clearly visible empty strip above a custom WPF title bar. Covering
            // that top frame matches Fluent Design and the pre-refactor behavior. HitTestResizeBorder uses the same
            // measured thickness, so the top edge remains resizable from the corresponding pixels inside the HWND.
            // DWM continues to draw the Windows 11 outline and rounded corners over this client-covered edge.
            return Inset(windowRect, frame.Left, 0, frame.Right, frame.Bottom);
        }

        // Both client strategies give WPF the complete HWND. ClientBorder supplies its outline through the WPF
        // template, while Windows11Client retains a minimal extended DWM frame so Windows can draw the native
        // outline and rounded outer corners even when no system backdrop material is active.
        return windowRect;
    }

    /// <summary>
    /// Calculates the Win11 DWM frame extension required by the active client and backdrop strategies.
    /// </summary>
    /// <remarks>
    /// A system backdrop requires the complete frame to be extended through the client area. Without a backdrop,
    /// a fully client-rendered Win11 window still needs one physical frame pixel: resetting all margins to zero
    /// makes DWM stop drawing its outline and can also make the window ineligible for rounded corners. Other frame
    /// strategies already retain real non-client pixels and therefore do not require this bridge.
    /// </remarks>
    internal static MARGINS CalculateDwmFrameMargins(
        FluentWindowFrameStrategy strategy,
        bool isBackdropApplied
    )
    {
        if (isBackdropApplied)
        {
            return new MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1,
            };
        }

        if (strategy == FluentWindowFrameStrategy.Windows11Client)
        {
            return new MARGINS
            {
                cxLeftWidth = 1,
                cxRightWidth = 1,
                cyTopHeight = 1,
                cyBottomHeight = 1,
            };
        }

        return default;
    }

    /// <summary>
    /// Determines whether FluentWindow must explicitly apply the DWM accent border color.
    /// </summary>
    /// <remarks>
    /// A Win11 window that retains native frame margins already follows the operating-system accent policy. The
    /// explicit color is needed only when the client covers the HWND: DWM continues to draw the one-pixel extended
    /// frame, but no longer changes that frame to the accent color on activation. High contrast always delegates
    /// back to the system default instead of introducing a potentially inaccessible custom color.
    /// </remarks>
    internal static bool ShouldApplyDwmAccentBorder(
        bool useNativeWindowBorder,
        bool isNcActive,
        bool isAccentOnTitleBarAndBorder,
        bool isHighContrast
    ) => !useNativeWindowBorder && isNcActive && isAccentOnTitleBarAndBorder && !isHighContrast;

    /// <summary>
    /// Converts the DWM colorization format (<c>0xAARRGGBB</c>) to Win32 <c>COLORREF</c>
    /// (<c>0x00BBGGRR</c>).
    /// </summary>
    internal static uint ConvertDwmColorizationColorToColorRef(uint colorizationColor)
    {
        var red   = (colorizationColor >> 16) & 0xFF;
        var green = (colorizationColor >> 8) & 0xFF;
        var blue  = colorizationColor & 0xFF;

        return red | (green << 8) | (blue << 16);
    }

    /// <summary>
    /// Calculates the WPF border used to present the selected native frame strategy.
    /// </summary>
    /// <remarks>
    /// The value is kept separate from <see cref="CalculateClientRect"/> because the native calculation uses
    /// physical pixels while WPF's <see cref="Thickness"/> is expressed in device-independent units. A maximized
    /// window never draws the client border: <c>WM_NCCALCSIZE</c> already reserves the measured native frame needed
    /// to keep content inside the monitor work area.
    /// </remarks>
    internal static Thickness CalculateVisualBorderThickness(
        FluentWindowFrameStrategy strategy,
        WindowState windowState
    )
    {
        if (windowState == WindowState.Maximized)
        {
            return new Thickness(0);
        }

        return strategy switch
        {
            FluentWindowFrameStrategy.Windows10Hybrid => new Thickness(0, 1, 0, 0),
            FluentWindowFrameStrategy.ClientBorder    => new Thickness(1),
            _ => new Thickness(0),
        };
    }

    /// <summary>
    /// Maps a semantic title-bar role to the native hit-test value consumed by Windows.
    /// </summary>
    /// <param name="role">Role inherited from the WPF title-bar visual tree.</param>
    /// <param name="isIconInteractionEnabled">
    /// Whether the application icon may expose icon-specific non-client interactions such as double-click close.
    /// </param>
    /// <returns>The corresponding HT value, or <see cref="IntPtr.Zero"/> for <see cref="TitleBarHitTestRole.None"/>.</returns>
    internal static IntPtr MapTitleBarRole(TitleBarHitTestRole role, bool isIconInteractionEnabled)
    {
        if (role == TitleBarHitTestRole.Icon && !isIconInteractionEnabled)
        {
            role = TitleBarHitTestRole.Caption;
        }

        return role switch
        {
            TitleBarHitTestRole.Client         => (IntPtr)PInvoke.HTCLIENT,
            TitleBarHitTestRole.Caption        => (IntPtr)PInvoke.HTCAPTION,
            TitleBarHitTestRole.Icon           => (IntPtr)PInvoke.HTSYSMENU,
            TitleBarHitTestRole.MinimizeButton => (IntPtr)PInvoke.HTMINBUTTON,
            TitleBarHitTestRole.MaximizeButton => (IntPtr)PInvoke.HTMAXBUTTON,
            TitleBarHitTestRole.CloseButton    => (IntPtr)PInvoke.HTCLOSE,
            TitleBarHitTestRole.HelpButton     => (IntPtr)PInvoke.HTHELP,
            _                                  => IntPtr.Zero,
        };
    }

    /// <summary>
    /// Maps a physical screen point to a resize edge or corner.
    /// </summary>
    internal static IntPtr HitTestResizeBorder(
        in RECT windowRect,
        in Point screenPoint,
        in WindowFrameInsets frame
    )
    {
        var row = 1;
        var column = 1;

        if (screenPoint.X >= windowRect.left && screenPoint.X < windowRect.left + frame.Left)
        {
            column = 0;
        }
        else if (screenPoint.X < windowRect.right && screenPoint.X >= windowRect.right - frame.Right)
        {
            column = 2;
        }

        if (screenPoint.Y >= windowRect.top && screenPoint.Y < windowRect.top + frame.Top)
        {
            row = 0;
        }
        else if (screenPoint.Y < windowRect.bottom && screenPoint.Y >= windowRect.bottom - frame.Bottom)
        {
            row = 2;
        }

        return HitTestBorders[row, column];
    }

    private static RECT Inset(in RECT rect, int left, int top, int right, int bottom)
    {
        return new RECT
        {
            left   = rect.left + left,
            top    = rect.top + top,
            right  = rect.right - right,
            bottom = rect.bottom - bottom,
        };
    }
}
