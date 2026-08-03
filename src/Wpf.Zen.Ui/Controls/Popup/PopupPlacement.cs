// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Exposes the actual target-relative direction selected by WPF for a <see cref="Popup"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Popup.Placement"/> is only a request. WPF evaluates multiple candidates and can place a Bottom Popup
/// above its target, for example, when the monitor has insufficient space below it. WPF keeps the selected candidate
/// private, so this behavior observes the final Popup HWND and compares its physical bounds with the target bounds.
/// No private WPF members are required.
/// </para>
/// <para>
/// Set <see cref="IsEnabledProperty"/> on a Popup and bind or trigger against
/// <see cref="ActualPlacementProperty"/>. The result is updated after opening and whenever the Popup HWND moves. The
/// read-only property inherits through the Popup's logical content tree, allowing Popup content to consume the same
/// value without crossing presentation-source boundaries manually.
/// </para>
/// <para>
/// Absolute and mouse placements do not have a stable element-relative target and therefore report
/// <see cref="PopupPlacementDirection.None"/>. Custom and relative placements are classified geometrically when a
/// placement target is available.
/// </para>
/// </remarks>
public static class PopupPlacement
{
    #pragma warning disable WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Identifies the attached property that enables actual-placement tracking for a Popup.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupPlacement),
            new PropertyMetadata(false, OnIsEnabledChanged)
        );

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        if (popup.GetValue(ControllerProperty) is PlacementController previousController)
        {
            previousController.Detach();
            popup.ClearValue(ControllerProperty);
        }

        if (e.NewValue is true)
        {
            var controller = new PlacementController(popup);
            popup.SetValue(ControllerProperty, controller);
            controller.Attach();
        }
    }

    /// <summary>
    /// Gets whether actual-placement tracking is enabled for the specified Popup.
    /// </summary>
    /// <param name="element">The Popup from which to read the attached property.</param>
    /// <returns>
    /// <see langword="true"/> when placement tracking is enabled; otherwise, <see langword="false"/>.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Enables or disables actual-placement tracking for the specified Popup.
    /// </summary>
    /// <param name="element">The Popup on which to set the attached property.</param>
    /// <param name="value">
    /// <see langword="true"/> to track its resolved direction; otherwise, <see langword="false"/>.
    /// </param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static readonly DependencyPropertyKey ActualPlacementPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "ActualPlacement",
            typeof(PopupPlacementDirection),
            typeof(PopupPlacement),
            new FrameworkPropertyMetadata(
                PopupPlacementDirection.None,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Identifies the read-only attached property that contains the Popup's resolved target-relative direction.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="PopupPlacementDirection.None"/>. The value returns to that default when the Popup
    /// closes or when tracking is disabled. This property inherits into the Popup content tree and does not affect
    /// layout or rendering by itself.
    /// </remarks>
    public static readonly DependencyProperty ActualPlacementProperty =
        ActualPlacementPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the resolved target-relative direction associated with a Popup or an element in its content tree.
    /// </summary>
    /// <param name="element">
    /// The Popup or Popup content element from which to read the attached property.
    /// </param>
    /// <returns>The current resolved placement direction.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    public static PopupPlacementDirection GetActualPlacement(DependencyObject element)
    {
        return (PopupPlacementDirection)element.GetValue(ActualPlacementProperty);
    }

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(PlacementController),
            typeof(PopupPlacement),
            new PropertyMetadata(null)
        );

    #pragma warning restore WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Bridges WPF's managed Popup lifecycle with the native window that contains PopupRoot.
    /// </summary>
    /// <remarks>
    /// Popup.Opened is raised after Popup.UpdatePosition and ShowWindow, so the initial HWND rectangle is final enough
    /// to classify immediately. The HWND hook is still required because WPF can later reposition the same window when
    /// its child size, placement, offsets, target, or DPI changes. Listening to the native position is both more
    /// complete and less invasive than duplicating every managed invalidation path.
    /// </remarks>
    private sealed class PlacementController
    {
        private readonly Popup _popup;
        private HwndSource? _popupSource;

        public PlacementController(Popup popup)
        {
            _popup = popup;
        }

        public void Attach()
        {
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;

            if (_popup.IsOpen)
            {
                AttachToPopupSourceAndUpdate();
            }
        }

        public void Detach()
        {
            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;
            DetachFromPopupSource();
            SetActualPlacement(PopupPlacementDirection.None);
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            AttachToPopupSourceAndUpdate();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            DetachFromPopupSource();
            SetActualPlacement(PopupPlacementDirection.None);
        }

        private void AttachToPopupSourceAndUpdate()
        {
            var source = _popup.Child is Visual child
                ? PresentationSource.FromVisual(child) as HwndSource
                : null;

            if (!ReferenceEquals(_popupSource, source))
            {
                DetachFromPopupSource();
                _popupSource = source;
                _popupSource?.AddHook(PopupWindowHook);
            }

            UpdateActualPlacement();
        }

        private void DetachFromPopupSource()
        {
            _popupSource?.RemoveHook(PopupWindowHook);
            _popupSource = null;
        }

        private IntPtr PopupWindowHook(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled
        )
        {
            if ((uint)message == PInvoke.WM_WINDOWPOSCHANGED)
            {
                // This notification is sent after SetWindowPos has committed the new rectangle, including WPF's
                // candidate flip and monitor-edge nudge. SetValue is skipped when the direction remains unchanged,
                // so a template trigger that adjusts Popup offsets cannot create a property-notification loop.
                UpdateActualPlacement();
            }

            return IntPtr.Zero;
        }

        private void UpdateActualPlacement()
        {
            if (
                !_popup.IsOpen
                || _popupSource is null
                || _popupSource.Handle == IntPtr.Zero
                || !TryGetPlacementTargetBounds(_popup, out var targetBounds)
                || !PInvoke.GetWindowRect((HWND)_popupSource.Handle, out var popupRectangle)
            )
            {
                SetActualPlacement(PopupPlacementDirection.None);
                return;
            }

            var popupBounds = new Rect(
                popupRectangle.left,
                popupRectangle.top,
                popupRectangle.Width,
                popupRectangle.Height
            );

            SetActualPlacement(ClassifyPlacement(_popup.Placement, popupBounds, targetBounds));
        }

        private void SetActualPlacement(PopupPlacementDirection value)
        {
            if (GetActualPlacement(_popup) != value)
            {
                _popup.SetValue(ActualPlacementPropertyKey, value);
            }
        }
    }

    /// <summary>
    /// Gets the rectangle used by WPF as the target-relative placement reference in physical screen pixels.
    /// </summary>
    /// <remarks>
    /// Popup permits PlacementTarget to be omitted, in which case its closest visual parent is the implicit target.
    /// All four corners are transformed independently because a render transform can rotate or skew the target; a
    /// two-corner shortcut would produce invalid bounds in those cases.
    /// </remarks>
    private static bool TryGetPlacementTargetBounds(Popup popup, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (popup.Placement is
            PlacementMode.Absolute or
            PlacementMode.AbsolutePoint or
            PlacementMode.Mouse or
            PlacementMode.MousePoint)
        {
            return false;
        }

        var target = popup.PlacementTarget ?? VisualTreeHelper.GetParent(popup) as UIElement;
        if (target is null || PresentationSource.FromVisual(target) is null)
        {
            return false;
        }

        var placementRectangle = popup.PlacementRectangle;
        if (placementRectangle == Rect.Empty)
        {
            placementRectangle = new Rect(target.RenderSize);
        }

        try
        {
            var topLeft = target.PointToScreen(placementRectangle.TopLeft);
            var topRight = target.PointToScreen(placementRectangle.TopRight);
            var bottomLeft = target.PointToScreen(placementRectangle.BottomLeft);
            var bottomRight = target.PointToScreen(placementRectangle.BottomRight);

            var left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

            bounds = new Rect(new Point(left, top), new Point(right, bottom));
            return !bounds.IsEmpty;
        }
        catch (InvalidOperationException)
        {
            // PointToScreen throws while a visual is disconnected or its PresentationSource is being replaced.
            // The next native move/open notification retries; a transient tree change must not break Popup usage.
            return false;
        }
    }

    /// <summary>
    /// Classifies the final HWND rectangle while preserving WPF's primary placement-axis semantics.
    /// </summary>
    private static PopupPlacementDirection ClassifyPlacement(
        PlacementMode requestedPlacement,
        Rect popupBounds,
        Rect targetBounds
    )
    {
        switch (requestedPlacement)
        {
            case PlacementMode.Top:
            case PlacementMode.Bottom:
                return GetVerticalPlacement(popupBounds, targetBounds);

            case PlacementMode.Left:
            case PlacementMode.Right:
                return GetHorizontalPlacement(popupBounds, targetBounds);

            case PlacementMode.Center:
                return PopupPlacementDirection.Center;

            case PlacementMode.Relative:
            case PlacementMode.RelativePoint:
            case PlacementMode.Custom:
                return GetNearestPlacement(popupBounds, targetBounds);

            default:
                return PopupPlacementDirection.None;
        }
    }

    private static PopupPlacementDirection GetVerticalPlacement(Rect popupBounds, Rect targetBounds)
    {
        var belowDistance = Math.Abs(popupBounds.Top - targetBounds.Bottom);
        var aboveDistance = Math.Abs(popupBounds.Bottom - targetBounds.Top);

        return aboveDistance < belowDistance
            ? PopupPlacementDirection.Top
            : PopupPlacementDirection.Bottom;
    }

    private static PopupPlacementDirection GetHorizontalPlacement(Rect popupBounds, Rect targetBounds)
    {
        var rightDistance = Math.Abs(popupBounds.Left - targetBounds.Right);
        var leftDistance = Math.Abs(popupBounds.Right - targetBounds.Left);

        return leftDistance < rightDistance
            ? PopupPlacementDirection.Left
            : PopupPlacementDirection.Right;
    }

    private static PopupPlacementDirection GetNearestPlacement(Rect popupBounds, Rect targetBounds)
    {
        if (popupBounds.IntersectsWith(targetBounds))
        {
            return PopupPlacementDirection.Center;
        }

        var topDistance = Math.Abs(popupBounds.Bottom - targetBounds.Top);
        var bottomDistance = Math.Abs(popupBounds.Top - targetBounds.Bottom);
        var leftDistance = Math.Abs(popupBounds.Right - targetBounds.Left);
        var rightDistance = Math.Abs(popupBounds.Left - targetBounds.Right);
        var nearestDistance = Math.Min(
            Math.Min(topDistance, bottomDistance),
            Math.Min(leftDistance, rightDistance)
        );

        if (Math.Abs(nearestDistance - topDistance) < 0.0001D)
        {
            return PopupPlacementDirection.Top;
        }

        if (Math.Abs(nearestDistance - bottomDistance) < 0.0001D)
        {
            return PopupPlacementDirection.Bottom;
        }

        return Math.Abs(nearestDistance - leftDistance) < 0.0001D
            ? PopupPlacementDirection.Left
            : PopupPlacementDirection.Right;
    }
}
