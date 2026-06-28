// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;

namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a <see cref="Popup"/> that can tolerate transparent placement overflow before visible content is nudged.
/// </summary>
/// <remarks>
/// <see cref="PlacementOverflowTolerance"/> describes the transparent outer thickness of the popup content that may
/// overflow outside the screen. The value must match the child layout margin used only for effects such as shadows;
/// the remaining inner rectangle is treated as the real visible content and is kept inside the monitor bounds
/// whenever its size allows it. Placement is corrected when the popup opens and once more after layout has loaded;
/// later target movement or content-size changes are not monitored so existing custom popup-follow behavior is not
/// overridden.
/// </remarks>
public class OverflowTolerantPopup : Popup
{
    /// <summary>
    /// Identifies the <see cref="PlacementOverflowTolerance"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PlacementOverflowToleranceProperty =
        DependencyProperty.Register(
            nameof(PlacementOverflowTolerance),
            typeof(Thickness),
            typeof(OverflowTolerantPopup),
            new PropertyMetadata(default(Thickness), OnPlacementOverflowToleranceChanged),
            ValidatePlacementOverflowTolerance
        );

    /// <summary>
    /// Gets or sets the transparent outer thickness that may remain outside the screen before the visible content is nudged.
    /// </summary>
    /// <remarks>
    /// This value is both the overflow tolerance and the inset from the popup HWND bounds to the visible content bounds.
    /// Set it to the same value as the child element's transparent effect margin.
    /// </remarks>
    public Thickness PlacementOverflowTolerance
    {
        get { return (Thickness)GetValue(PlacementOverflowToleranceProperty); }
        set { SetValue(PlacementOverflowToleranceProperty, value); }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // WPF may already have nudged the HWND by the time Opened is raised.
        ApplyPlacementOverflowTolerance();

        // Run once more after the popup child has completed its loaded layout pass. This catches templates
        // whose effect margin or measured size is finalized after the HWND is first positioned.
        _ = Dispatcher.BeginInvoke(ApplyPlacementOverflowTolerance, DispatcherPriority.Loaded);
    }

    private static bool ValidatePlacementOverflowTolerance(object? value)
    {
        if (value is not Thickness thickness)
        {
            return false;
        }

        return IsValidTolerance(thickness.Left)
            && IsValidTolerance(thickness.Top)
            && IsValidTolerance(thickness.Right)
            && IsValidTolerance(thickness.Bottom);

        static bool IsValidTolerance(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }
    }

    private static void OnPlacementOverflowToleranceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is OverflowTolerantPopup { IsOpen: true } popup)
        {
            popup.ApplyPlacementOverflowTolerance();
        }
    }

    private void ApplyPlacementOverflowTolerance()
    {
        if (!IsOpen || Child is not { } child || PlacementOverflowTolerance == default)
        {
            return;
        }

        if (PresentationSource.FromVisual(child) is not HwndSource hwndSource)
        {
            return;
        }

        if (hwndSource.Handle == IntPtr.Zero)
        {
            return;
        }

        var hwnd = (HWND)hwndSource.Handle;
        if (!PInvoke.GetWindowRect(hwnd, out var popupRect))
        {
            return;
        }

        var popupBounds = new Rect(
            popupRect.left,
            popupRect.top,
            popupRect.Width,
            popupRect.Height
        );

        var dpi       = GetDpi();
        var tolerance = DpiHelper.LogicalThicknessToDevice(
            PlacementOverflowTolerance,
            dpi.DpiScaleX,
            dpi.DpiScaleY
        );

        if (!TryGetDesiredPopupBounds(popupBounds, tolerance, out var desiredPopupBounds))
        {
            return;
        }

        // Use the monitor containing the placement target rather than the primary monitor. Taskbar reserved areas
        // are intentionally ignored here; Popup placement should react to screen edges, not the taskbar edge.
        // In mixed-DPI
        // multi-monitor setups this is still best-effort, because WPF and Win32 may report positions using
        // different DPI contexts while a window crosses monitor boundaries.
        var monitorBounds = UnsafeNativeMethods.GetMonitorBounds(GetPlacementReferenceBounds());
        if (Placement == PlacementMode.Custom)
        {
            desiredPopupBounds = RelaxPopupBoundsAtScreenEdges(desiredPopupBounds, tolerance, monitorBounds);
        }

        // Only the inner visible rectangle is clamped to the monitor bounds. The transparent outer rectangle,
        // usually reserved for shadow rendering, may remain outside the screen.
        desiredPopupBounds = ClampVisibleBoundsToScreen(desiredPopupBounds, tolerance, monitorBounds);

        if (Math.Abs(desiredPopupBounds.Left - popupBounds.Left) < 1D
         && Math.Abs(desiredPopupBounds.Top - popupBounds.Top) < 1D)
        {
            return;
        }

        const SET_WINDOW_POS_FLAGS flags =
              SET_WINDOW_POS_FLAGS.SWP_NOZORDER
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
            | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        _ = PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            (int)Math.Round(desiredPopupBounds.Left),
            (int)Math.Round(desiredPopupBounds.Top),
            0,
            0,
            flags
        );
    }

    /// <summary>
    /// Gets the DPI scale used to convert WPF logical placement values into Win32 device pixels.
    /// </summary>
    /// <remarks>
    /// The placement target is preferred because the popup is positioned relative to it. If the popup is opened
    /// without a target, the child visual is the best available fallback.
    /// </remarks>
    private DpiScale GetDpi()
    {
        if (PlacementTarget is Visual target)
        {
            return VisualTreeHelper.GetDpi(target);
        }

        return Child is Visual child
            ? VisualTreeHelper.GetDpi(child)
            : new DpiScale(1D, 1D);
    }

    /// <summary>
    /// Calculates the popup HWND bounds that would place the visible content at the requested placement point.
    /// </summary>
    /// <remarks>
    /// The returned bounds are expressed in physical screen pixels. The transparent tolerance is subtracted from
    /// the placement point so the visible content, not the HWND including shadow margin, aligns with the target.
    /// For <see cref="PlacementMode.Custom"/>, the original callback result cannot be reconstructed, so the current
    /// HWND bounds are used and later relaxed only when WPF pinned the popup to a screen edge.
    /// </remarks>
    private bool TryGetDesiredPopupBounds(Rect popupBounds, Thickness tolerance, out Rect bounds)
    {
        bounds = popupBounds;

        if (Placement is PlacementMode.Absolute or PlacementMode.AbsolutePoint)
        {
            var absoluteDpi = GetDpi();
            bounds = new Rect(
                (HorizontalOffset * absoluteDpi.DpiScaleX) - tolerance.Left,
                (VerticalOffset * absoluteDpi.DpiScaleY) - tolerance.Top,
                popupBounds.Width,
                popupBounds.Height
            );

            return true;
        }

        if (Placement is PlacementMode.Mouse or PlacementMode.MousePoint)
        {
            if (!PInvoke.GetCursorPos(out var cursorPoint))
            {
                return false;
            }

            var mouseDpi = GetDpi();
            bounds = new Rect(
                cursorPoint.X - tolerance.Left + (HorizontalOffset * mouseDpi.DpiScaleX),
                cursorPoint.Y - tolerance.Top + (VerticalOffset * mouseDpi.DpiScaleY),
                popupBounds.Width,
                popupBounds.Height
            );

            return true;
        }

        if (Placement == PlacementMode.Custom)
        {
            // CustomPopupPlacementCallback may use arbitrary logic. Preserve the callback result here and only
            // undo obvious screen-edge nudges in RelaxPopupBoundsAtScreenEdges.
            return true;
        }

        if (PlacementTarget is not FrameworkElement target)
        {
            return false;
        }

        if (target.RenderSize.Width <= 0D || target.RenderSize.Height <= 0D)
        {
            return false;
        }

        var dpi           = GetDpi();
        var targetBox     = GetPlacementTargetBounds(target);
        var offsetX       = HorizontalOffset * dpi.DpiScaleX;
        var offsetY       = VerticalOffset * dpi.DpiScaleY;
        var visibleWidth  = Math.Max(0D, popupBounds.Width - tolerance.Left - tolerance.Right);
        var visibleHeight = Math.Max(0D, popupBounds.Height - tolerance.Top - tolerance.Bottom);

        // WPF may flip Top to Bottom, Bottom to Top, Left to Right, or Right to Left before this correction runs.
        // Infer the resolved side from where the visible rectangle ended up relative to the placement target.
        var verticalEdge  = GetResolvedVerticalEdge(popupBounds, targetBox, tolerance, offsetY);
        var horizontalEdge = GetResolvedHorizontalEdge(
            popupBounds,
            targetBox,
            tolerance,
            offsetX
        );

        var position = Placement switch
        {
            PlacementMode.Top => new Point(
                targetBox.Left + offsetX - tolerance.Left,
                GetVerticalPosition(
                    targetBox,
                    popupBounds.Height,
                    tolerance,
                    offsetY,
                    verticalEdge
                )
            ),
            PlacementMode.Bottom => new Point(
                targetBox.Left + offsetX - tolerance.Left,
                GetVerticalPosition(
                    targetBox,
                    popupBounds.Height,
                    tolerance,
                    offsetY,
                    verticalEdge
                )
            ),
            PlacementMode.Left => new Point(
                GetHorizontalPosition(
                    targetBox,
                    popupBounds.Width,
                    tolerance,
                    offsetX,
                    horizontalEdge
                ),
                targetBox.Top + offsetY - tolerance.Top
            ),
            PlacementMode.Right => new Point(
                GetHorizontalPosition(
                    targetBox,
                    popupBounds.Width,
                    tolerance,
                    offsetX,
                    horizontalEdge
                ),
                targetBox.Top + offsetY - tolerance.Top
            ),
            PlacementMode.Center => new Point(
                targetBox.Left + ((targetBox.Width - visibleWidth) / 2D) + offsetX - tolerance.Left,
                targetBox.Top + ((targetBox.Height - visibleHeight) / 2D) + offsetY - tolerance.Top
            ),
            PlacementMode.Relative or PlacementMode.RelativePoint => new Point(
                targetBox.Left + offsetX - tolerance.Left,
                targetBox.Top + offsetY - tolerance.Top
            ),
            _ => default,
        };

        var isSupportedPlacement = Placement is PlacementMode.Top
            or PlacementMode.Bottom
            or PlacementMode.Left
            or PlacementMode.Right
            or PlacementMode.Center
            or PlacementMode.Relative
            or PlacementMode.RelativePoint;

        if (isSupportedPlacement)
        {
            bounds = new Rect(position, popupBounds.Size);
        }

        return isSupportedPlacement;
    }

    /// <summary>
    /// Infers whether WPF resolved the popup above or below the placement target.
    /// </summary>
    private static PopupVerticalEdge GetResolvedVerticalEdge(
        Rect popupBounds,
        Rect targetBox,
        Thickness tolerance,
        double offsetY
    )
    {
        var visibleBounds = GetVisibleBounds(popupBounds, tolerance);
        var belowDistance = Math.Abs(visibleBounds.Top - (targetBox.Bottom + offsetY));
        var aboveDistance = Math.Abs(visibleBounds.Bottom - (targetBox.Top + offsetY));

        return aboveDistance < belowDistance
            ? PopupVerticalEdge.Above
            : PopupVerticalEdge.Below;
    }

    /// <summary>
    /// Infers whether WPF resolved the popup to the left or right of the placement target.
    /// </summary>
    private static PopupHorizontalEdge GetResolvedHorizontalEdge(
        Rect popupBounds,
        Rect targetBox,
        Thickness tolerance,
        double offsetX
    )
    {
        var visibleBounds = GetVisibleBounds(popupBounds, tolerance);
        var rightDistance = Math.Abs(visibleBounds.Left - (targetBox.Right + offsetX));
        var leftDistance  = Math.Abs(visibleBounds.Right - (targetBox.Left + offsetX));

        return leftDistance < rightDistance
            ? PopupHorizontalEdge.Left
            : PopupHorizontalEdge.Right;
    }

    /// <summary>
    /// Calculates the HWND top edge for a popup whose visible content is above or below the target.
    /// </summary>
    private static double GetVerticalPosition(
        Rect targetBox,
        double popupHeight,
        Thickness tolerance,
        double offsetY,
        PopupVerticalEdge edge
    )
    {
        return edge == PopupVerticalEdge.Above
            ? targetBox.Top - popupHeight + tolerance.Bottom + offsetY
            : targetBox.Bottom + offsetY - tolerance.Top;
    }

    /// <summary>
    /// Calculates the HWND left edge for a popup whose visible content is left or right of the target.
    /// </summary>
    private static double GetHorizontalPosition(
        Rect targetBox,
        double popupWidth,
        Thickness tolerance,
        double offsetX,
        PopupHorizontalEdge edge
    )
    {
        return edge == PopupHorizontalEdge.Left
            ? targetBox.Left - popupWidth + tolerance.Right + offsetX
            : targetBox.Right + offsetX - tolerance.Left;
    }

    /// <summary>
    /// Returns the real visible content bounds by removing the transparent overflow margin from the HWND bounds.
    /// </summary>
    private static Rect GetVisibleBounds(Rect popupBounds, Thickness tolerance)
    {
        return new Rect(
            popupBounds.Left + tolerance.Left,
            popupBounds.Top + tolerance.Top,
            Math.Max(0D, popupBounds.Width - tolerance.Left - tolerance.Right),
            Math.Max(0D, popupBounds.Height - tolerance.Top - tolerance.Bottom)
        );
    }

    /// <summary>
    /// Gets the screen-space rectangle used to choose the monitor bounds for placement correction.
    /// </summary>
    private Rect GetPlacementReferenceBounds()
    {
        if (PlacementTarget is FrameworkElement target && target.RenderSize.Width > 0D && target.RenderSize.Height > 0D)
        {
            return GetPlacementTargetBounds(target);
        }

        if (PInvoke.GetCursorPos(out var cursorPoint))
        {
            return new Rect(cursorPoint.X, cursorPoint.Y, 1D, 1D);
        }

        return Rect.Empty;
    }

    /// <summary>
    /// Moves the popup just enough for the visible content bounds to fit inside the monitor bounds.
    /// </summary>
    /// <remarks>
    /// If the visible content is larger than the monitor on an axis, the start edge is aligned to the monitor
    /// and the far edge is allowed to overflow; no horizontal or vertical move can make oversized content fit fully.
    /// </remarks>
    private static Rect ClampVisibleBoundsToScreen(Rect popupBounds, Thickness tolerance, Rect screenBounds)
    {
        var visibleBounds = GetVisibleBounds(popupBounds, tolerance);
        var offsetX       = GetClampOffset(visibleBounds.Left, visibleBounds.Right, visibleBounds.Width, screenBounds.Left, screenBounds.Right, screenBounds.Width);
        var offsetY       = GetClampOffset(visibleBounds.Top, visibleBounds.Bottom, visibleBounds.Height, screenBounds.Top, screenBounds.Bottom, screenBounds.Height);

        return new Rect(
            popupBounds.Left + offsetX,
            popupBounds.Top + offsetY,
            popupBounds.Width,
            popupBounds.Height
        );
    }

    /// <summary>
    /// Best-effort correction for <see cref="PlacementMode.Custom"/> when WPF pinned the whole HWND to a screen edge.
    /// </summary>
    /// <remarks>
    /// Because a custom placement callback can return arbitrary coordinates, this method does not recompute the
    /// original callback placement. It only releases transparent overflow when the current HWND appears to be nudged
    /// exactly against the monitor edge.
    /// </remarks>
    private static Rect RelaxPopupBoundsAtScreenEdges(Rect popupBounds, Thickness tolerance, Rect screenBounds)
    {
        const double EdgeTolerance = 1D;

        var x = popupBounds.Left;
        var y = popupBounds.Top;

        if (Math.Abs(popupBounds.Left - screenBounds.Left) < EdgeTolerance)
        {
            x -= tolerance.Left;
        }
        else if (Math.Abs(popupBounds.Right - screenBounds.Right) < EdgeTolerance)
        {
            x += tolerance.Right;
        }

        if (Math.Abs(popupBounds.Top - screenBounds.Top) < EdgeTolerance)
        {
            y -= tolerance.Top;
        }
        else if (Math.Abs(popupBounds.Bottom - screenBounds.Bottom) < EdgeTolerance)
        {
            y += tolerance.Bottom;
        }

        return new Rect(x, y, popupBounds.Width, popupBounds.Height);
    }

    /// <summary>
    /// Gets the target placement rectangle in physical screen pixels.
    /// </summary>
    private Rect GetPlacementTargetBounds(FrameworkElement target)
    {
        var placementRectangle = PlacementRectangle;
        if (placementRectangle == Rect.Empty)
        {
            placementRectangle = new Rect(target.RenderSize);
        }

        var topLeft     = target.PointToScreen(placementRectangle.TopLeft);
        var bottomRight = target.PointToScreen(placementRectangle.BottomRight);

        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    /// Calculates the axis offset needed to keep the visible interval inside the screen interval.
    /// </summary>
    private static double GetClampOffset(
        double visibleStart,
        double visibleEnd,
        double visibleLength,
        double screenStart,
        double screenEnd,
        double screenLength
    )
    {
        if (visibleLength > screenLength)
        {
            return screenStart - visibleStart;
        }

        if (visibleStart < screenStart)
        {
            return screenStart - visibleStart;
        }

        if (visibleEnd > screenEnd)
        {
            return screenEnd - visibleEnd;
        }

        return 0D;
    }

    private enum PopupVerticalEdge
    {
        Above,
        Below,
    }

    private enum PopupHorizontalEdge
    {
        Left,
        Right,
    }
}
