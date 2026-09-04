// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Security;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Tracks and exposes the actual target-relative direction selected by WPF for a <see cref="Popup"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Popup.Placement"/> is only a request. WPF evaluates multiple candidates and can place a Bottom Popup
/// above its target, for example, when the monitor has insufficient space below it. WPF keeps the selected candidate
/// private. For Top/Bottom/Left/Right, this behavior reads WPF's cached candidate flags through reflection, before
/// monitor-edge nudging. These describe logical candidate sides, not screen-space sides after transforms or offsets.
/// When private state is unavailable, the final HWND bounds provide an estimate instead.
/// This tracker observes placement only; it does not select a placement mode or change the Popup's offsets.
/// </para>
/// <para>
/// Set <see cref="IsEnabledProperty"/> on a Popup and bind or trigger against
/// <see cref="ActualPlacementProperty"/>. The result is updated after opening and whenever the Popup HWND moves. The
/// read-only property inherits through the Popup's logical content tree, allowing Popup content to consume the same
/// value without crossing presentation-source boundaries manually. Placement input and layout changes also schedule
/// an update, because WPF can select a different candidate without moving the HWND.
/// </para>
/// <para>
/// Absolute and mouse placements do not have a stable element-relative target and therefore report
/// <see cref="PopupPlacementDirection.None"/>. Custom and relative placements are classified geometrically when a
/// placement target is available. Inspect <see cref="IsPlacementEstimatedProperty"/> to distinguish those estimates
/// from WPF candidate results. Mouse modes treated internally as Bottom follow the Bottom candidate rules.
/// </para>
/// </remarks>
public static class PopupPlacementTracker
{
    #pragma warning disable WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Identifies the attached property that enables actual-placement tracking for a Popup.
    /// </summary>
    /// <remarks>
    /// Defaults to false and does not inherit or invalidate layout. Its change callback attaches or releases the
    /// controller and resets the published result when disabled; it is intended as configuration, not animation.
    /// </remarks>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupPlacementTracker),
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
            typeof(PopupPlacementTracker),
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

    private static readonly DependencyPropertyKey IsPlacementEstimatedPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsPlacementEstimated",
            typeof(bool),
            typeof(PopupPlacementTracker),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>
    /// Identifies whether the current direction is inferred from window geometry rather than WPF's candidate state.
    /// </summary>
    /// <remarks>
    /// Defaults to false, including when no direction is available. Inherits into Popup content and does not itself
    /// invalidate layout or rendering. This read-only value is not an animation input. Custom/relative placements
    /// and reflection failures produce estimates; consumers requiring a candidate result should reject those values.
    /// </remarks>
    public static readonly DependencyProperty IsPlacementEstimatedProperty =
        IsPlacementEstimatedPropertyKey.DependencyProperty;

    /// <summary>Gets whether the associated placement direction is a geometric estimate.</summary>
    /// <param name="element">The Popup or an element in its content tree.</param>
    /// <returns>True for an estimated direction; false for a candidate result or no result.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    public static bool GetIsPlacementEstimated(DependencyObject element)
    {
        return (bool)element.GetValue(IsPlacementEstimatedProperty);
    }

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(PlacementController),
            typeof(PopupPlacementTracker),
            new PropertyMetadata(null)
        );

    #pragma warning restore WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Bridges WPF's managed Popup lifecycle with the native window that contains PopupRoot.
    /// </summary>
    /// <remarks>
    /// Popup.Opened is raised after Popup.UpdatePosition and ShowWindow, so the initial HWND rectangle is final enough
    /// to classify immediately. The HWND hook is still required because WPF can later reposition the same window when
    /// its child size, placement, offsets, target, or DPI changes. Managed notifications cover candidate changes
    /// that leave the final HWND coordinates unchanged. Deferred reads avoid publishing state inside UpdatePosition.
    /// </remarks>
    private sealed class PlacementController
    {
        private readonly Popup                _popup;
        private          HwndSource?          _popupSource;
        private          DispatcherOperation? _pendingUpdate;
        private          bool                 _observing;

        private static readonly DependencyPropertyDescriptor[] PlacementInputs =
            new[]
            {
                Popup.PlacementProperty,
                Popup.PlacementTargetProperty,
                Popup.PlacementRectangleProperty,
                Popup.HorizontalOffsetProperty,
                Popup.VerticalOffsetProperty,
                Popup.ChildProperty,
                Popup.CustomPopupPlacementCallbackProperty,
                FrameworkElement.FlowDirectionProperty,
            }.Select(
                property => DependencyPropertyDescriptor.FromProperty(property, typeof(Popup))
            ).ToArray();

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
            if (!_observing)
            {
                _observing = true;

                _popup.LayoutUpdated += OnPlacementInvalidated;

                foreach (var input in PlacementInputs)
                {
                    input.AddValueChanged(_popup, OnPlacementInvalidated);
                }
            }

            var source = _popup.Child is Visual child
                ? PresentationSource.FromVisual(child) as HwndSource
                : null;

            if (!ReferenceEquals(_popupSource, source))
            {
                _popupSource?.RemoveHook(PopupWindowHook);
                _popupSource = source;
                _popupSource?.AddHook(PopupWindowHook);
            }

            UpdateActualPlacement();
        }

        private void DetachFromPopupSource()
        {
            if (_observing)
            {
                _observing = false;

                _popup.LayoutUpdated -= OnPlacementInvalidated;

                foreach (var input in PlacementInputs)
                {
                    input.RemoveValueChanged(_popup, OnPlacementInvalidated);
                }
            }

            _pendingUpdate?.Abort();
            _pendingUpdate = null;

            _popupSource?.RemoveHook(PopupWindowHook);
            _popupSource = null;
        }

        private void OnPlacementInvalidated(object? sender, EventArgs e)
        {
            // LayoutUpdated covers child/target transforms and layout. Property descriptors also cover changes
            // that do not invalidate layout. Coalesce both after WPF's render/layout work, without a polling timer.
            if (!_observing || _pendingUpdate is not null)
            {
                return;
            }

            _pendingUpdate = _popup.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                    {
                        _pendingUpdate = null;
                        AttachToPopupSourceAndUpdate();
                    }
                )
            );
        }

        private IntPtr PopupWindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == PInvoke.WM_WINDOWPOSCHANGED)
            {
                // SetWindowPos can be nested inside WPF positioning. Publishing later avoids reentering that
                // computation when an ActualPlacement trigger changes the Popup's offsets or content size.
                OnPlacementInvalidated(this, EventArgs.Empty);
            }

            return IntPtr.Zero;
        }

        private void UpdateActualPlacement()
        {
            if (!_popup.IsOpen || _popupSource is null || _popupSource.Handle == IntPtr.Zero)
            {
                SetActualPlacement(PopupPlacementDirection.None);
                return;
            }

            var placement = CandidateState.GetEffectivePlacement(_popup);
            if (!TryGetPlacementTargetBounds(_popup, placement, out var targetBounds))
            {
                SetActualPlacement(PopupPlacementDirection.None);
                return;
            }

            if (CandidateState.TryGetDirection(_popup, placement, out var direction))
            {
                SetActualPlacement(direction);
                return;
            }

            if (!PInvoke.GetWindowRect((HWND)_popupSource.Handle, out var popupRectangle))
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

            SetActualPlacement(ClassifyPlacement(placement, popupBounds, targetBounds), estimated: true);
        }

        private void SetActualPlacement(PopupPlacementDirection value, bool estimated = false)
        {
            estimated &= value != PopupPlacementDirection.None;
            if (GetIsPlacementEstimated(_popup) != estimated)
            {
                _popup.SetValue(IsPlacementEstimatedPropertyKey, estimated);
            }

            if (GetActualPlacement(_popup) != value)
            {
                _popup.SetValue(ActualPlacementPropertyKey, value);
            }
        }
    }

    /// <summary>
    /// Reads the candidate anchors WPF retains for animation; the public Placement property only exposes a request.
    /// </summary>
    /// <remarks>
    /// Popup.UpdatePosition writes AnimateFromBottom/Right whenever a better candidate is selected, even with
    /// PopupAnimation.None, before nudging to monitor bounds. GetPointCombination maps these flags to logical sides
    /// only for the four directional modes. Custom candidates do not retain their selected index. FlowDirection and
    /// transforms can change physical anchor coordinates, so these flags must not be described as screen directions.
    /// Private names/semantics depend on the WPF implementation. Cache discovery and tolerate missing/inaccessible
    /// getters; callers fall back to explicitly marked geometry estimates instead of failing Popup usage.
    /// </remarks>
    private static class CandidateState
    {
        private static readonly PropertyInfo? EffectivePlacement = FindProperty("PlacementInternal");
        private static readonly PropertyInfo? FromBottom = FindProperty("AnimateFromBottom");
        private static readonly PropertyInfo? FromRight = FindProperty("AnimateFromRight");

        private static PropertyInfo? FindProperty(string name)
        {
            try
            {
                return typeof(Popup).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
            }
            catch (AmbiguousMatchException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        private static bool TryRead<T>(Popup popup, PropertyInfo? property, out T value)
        {
            try
            {
                if (property?.GetValue(popup) is T result)
                {
                    value = result;
                    return true;
                }
            }
            catch (TargetInvocationException)
            {
                // Unavailable private state is handled by the geometric fallback.
            }
            catch (MemberAccessException)
            {
                // Unavailable private state is handled by the geometric fallback.
            }
            catch (SecurityException)
            {
                // Unavailable private state is handled by the geometric fallback.
            }
            catch (ArgumentException)
            {
                // Unavailable private state is handled by the geometric fallback.
            }
            catch (TargetException)
            {
                // Unavailable private state is handled by the geometric fallback.
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Gets WPF's effective input mode, including keyboard ToolTips that treat mouse placement as Bottom.
        /// Uses the public request when the internal getter is unavailable.
        /// </summary>
        public static PlacementMode GetEffectivePlacement(Popup popup)
        {
            return TryRead<PlacementMode>(popup, EffectivePlacement, out var placement) ? placement : popup.Placement;
        }

        /// <summary>
        /// Resolves supported logical candidate sides after positioning. Returns false for unsupported modes or
        /// inaccessible flags so the caller can publish a marked estimate. Center has a single public candidate.
        /// </summary>
        public static bool TryGetDirection(Popup popup, PlacementMode placement, out PopupPlacementDirection direction)
        {
            direction = PopupPlacementDirection.None;

            switch (placement)
            {
                case PlacementMode.Top:
                case PlacementMode.Bottom:
                    if (!TryRead<bool>(popup, FromBottom, out var fromBottom))
                    {
                        return false;
                    }

                    direction = fromBottom ? PopupPlacementDirection.Top : PopupPlacementDirection.Bottom;
                    return true;

                case PlacementMode.Left:
                case PlacementMode.Right:
                    if (!TryRead<bool>(popup, FromRight, out var fromRight))
                    {
                        return false;
                    }

                    direction = fromRight ? PopupPlacementDirection.Left : PopupPlacementDirection.Right;
                    return true;

                case PlacementMode.Center:
                    direction = PopupPlacementDirection.Center;
                    return true;

                default:
                    return false;
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
    private static bool TryGetPlacementTargetBounds(Popup popup, PlacementMode placement, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (placement is
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
            var topLeft     = target.PointToScreen(placementRectangle.TopLeft);
            var topRight    = target.PointToScreen(placementRectangle.TopRight);
            var bottomLeft  = target.PointToScreen(placementRectangle.BottomLeft);
            var bottomRight = target.PointToScreen(placementRectangle.BottomRight);

            var left   = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var top    = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var right  = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
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

        var topDistance    = Math.Abs(popupBounds.Bottom - targetBounds.Top);
        var bottomDistance = Math.Abs(popupBounds.Top - targetBounds.Bottom);
        var leftDistance   = Math.Abs(popupBounds.Right - targetBounds.Left);
        var rightDistance  = Math.Abs(popupBounds.Left - targetBounds.Right);

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
