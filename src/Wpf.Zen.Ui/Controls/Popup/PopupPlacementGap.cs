// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Data;
using NativeContextMenu = System.Windows.Controls.ContextMenu;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Adds direction-aware spacing between a <see cref="Popup"/> and its placement target.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Popup.VerticalOffset"/> always translates the placement rectangle downward in screen
/// coordinates. It
/// therefore creates a gap for a Bottom candidate but an overlap for the Top candidate selected by
/// WPF near a monitor
/// edge. Changing the offset after observing that selected side feeds a new value back into WPF's
/// candidate scoring
/// and can make the Popup alternate indefinitely between Top and Bottom.
/// </para>
/// <para>
/// This behavior represents vertical spacing by expanding <see cref="Popup.PlacementRectangle"/>
/// equally at its top
/// and bottom. Both candidates are consequently scored with the same gap in one positioning pass,
/// so the selected
/// side does not alter any of its own inputs. Vertical expansion applies to Top and Bottom
/// placement;
/// horizontal expansion applies to Left and Right placement. Other modes retain their original
/// placement rectangle. Both gaps may be configured together; Placement selects the applicable
/// axis.
/// A caller can still use Popup.HorizontalOffset and Popup.VerticalOffset independently for a
/// direction-independent translation.
/// </para>
/// </remarks>
public static class PopupPlacementGap
{
    #pragma warning disable WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Identifies the attached property that specifies equal spacing above and below a Popup's
    /// placement target.
    /// </summary>
    /// <remarks>
    /// The default is zero. Values must be finite and non-negative. The value is expressed in
    /// device-independent
    /// units and participates in WPF's normal placement scoring through an expanded placement
    /// rectangle; it does not
    /// affect the target's layout. Setting the property on a <see cref="Flyout"/> is supported;
    /// the resulting expanded
    /// placement rectangle flows through Flyout's normal binding to its internal Popup.
    /// </remarks>
    public static readonly DependencyProperty VerticalProperty
        = DependencyProperty.RegisterAttached(
            "Vertical",
            typeof(double),
            typeof(PopupPlacementGap),
            new PropertyMetadata(0D, OnGapChanged),
            ValidateGap
        );

    /// <summary>
    /// Gets the equal vertical placement gap associated with the specified object.
    /// </summary>
    /// <param name="element">The Popup or Flyout from which to read the value.</param>
    /// <returns>The non-negative spacing in device-independent units.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(Flyout))]
    public static double GetVertical(DependencyObject element)
    {
        return (double)element.GetValue(VerticalProperty);
    }

    /// <summary>
    /// Sets the equal vertical placement gap associated with the specified object.
    /// </summary>
    /// <param name="element">The Popup or Flyout on which to set the value.</param>
    /// <param name="value">The finite, non-negative spacing in device-independent units.</param>
    public static void SetVertical(DependencyObject element, double value)
    {
        element.SetValue(VerticalProperty, value);
    }

    /// <summary>
    /// Identifies equal spacing to the left and right of a Popup's placement target.
    /// </summary>
    /// <remarks>
    /// The default is zero. Values must be finite, non-negative device-independent units.
    /// Applies to Left and Right placement by expanding the placement rectangle on both sides.
    /// WPF evaluates both candidates with this gap, including when placement flips at a screen
    /// edge.
    /// Supports Popup and Flyout; the target's layout is unaffected.
    /// </remarks>
    public static readonly DependencyProperty HorizontalProperty
        = DependencyProperty.RegisterAttached(
            "Horizontal",
            typeof(double),
            typeof(PopupPlacementGap),
            new PropertyMetadata(0D, OnGapChanged),
            ValidateGap
        );

    /// <summary>
    /// Gets the equal horizontal placement gap associated with the specified object.
    /// </summary>
    /// <param name="element">The Popup or Flyout from which to read the value.</param>
    /// <returns>The non-negative spacing in device-independent units.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(Flyout))]
    public static double GetHorizontal(DependencyObject element)
    {
        return (double)element.GetValue(HorizontalProperty);
    }

    /// <summary>
    /// Sets the equal horizontal placement gap associated with the specified object.
    /// </summary>
    /// <param name="element">The Popup or Flyout on which to set the value.</param>
    /// <param name="value">The finite, non-negative spacing in device-independent units.</param>
    public static void SetHorizontal(DependencyObject element, double value)
    {
        element.SetValue(HorizontalProperty, value);
    }

    private static readonly DependencyProperty ControllerProperty
        = DependencyProperty.RegisterAttached(
            "Controller",
            typeof(PlacementRectangleController),
            typeof(PopupPlacementGap),
            new PropertyMetadata(null)
        );

    #pragma warning restore WPF0061 // Accessor method for attached property should have standard documentation text

    private static bool ValidateGap(object? value)
    {
        return value is double gap and >= 0D && !double.IsInfinity(gap);
    }

    /// <summary>
    /// Recomputes spacing while retaining the shared controller until both gaps are disabled.
    /// </summary>
    private static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not Flyout)
        {
            // Other DependencyObjects retain the value without side effects, which is consistent with normal
            // attached-property behavior.
            return;
        }

        if (d.GetValue(ControllerProperty) is not PlacementRectangleController controller)
        {
            if ((double)e.NewValue <= 0D)
            {
                return;
            }

            controller = new PlacementRectangleController(d);
            d.SetCurrentValue(ControllerProperty, controller);
            controller.Attach();
        }

        controller.IsGapEnabled = GetVertical(d) > 0D || GetHorizontal(d) > 0D;
        controller.UpdatePlacementRectangle();

        ReleaseControllerIfUnused(d, controller);
    }

    /// <summary>
    /// Enables the standard alignment-property contribution for a Popup, ContextMenu or Flyout.
    /// </summary>
    /// <remarks>
    /// Alignment and placement gaps both transform PlacementRectangle. Keeping them in one
    /// controller preserves the
    /// consumer's authored rectangle and prevents the two transformations from treating each
    /// other's effective value
    /// as new input.
    /// </remarks>
    internal static void SetAlignmentEnabled(DependencyObject owner, bool isEnabled)
    {
        if (owner is not Popup and not NativeContextMenu and not Flyout)
        {
            return;
        }

        if (owner.GetValue(ControllerProperty) is not PlacementRectangleController controller)
        {
            if (!isEnabled)
            {
                return;
            }

            controller = new PlacementRectangleController(owner);
            owner.SetCurrentValue(ControllerProperty, controller);
            controller.Attach();
        }

        controller.IsAlignmentEnabled = isEnabled;
        controller.UpdatePlacementRectangle();

        ReleaseControllerIfUnused(owner, controller);
    }

    private static void ReleaseControllerIfUnused(
        DependencyObject owner,
        PlacementRectangleController controller)
    {
        if (controller.IsGapEnabled || controller.IsAlignmentEnabled)
        {
            return;
        }

        controller.Detach();
        owner.ClearValue(ControllerProperty);
    }

    /// <summary>
    /// Composes placement-rectangle features while preserving the caller's unmodified value for
    /// restoration.
    /// </summary>
    private sealed class PlacementRectangleController
    {
        private readonly DependencyObject       _owner;
        private readonly Popup?                 _popup;
        private readonly Flyout?                _flyout;
        private readonly NativeContextMenu?     _contextMenu;
        private readonly DependencyProperty     _placementRectangleProperty;
        private readonly DependencyProperty     _placementProperty;
        private readonly List<PropertyObserver> _propertyObservers = [];
        private readonly DependencyProperty     _placementTargetProperty;

        private UIElement?      _observedTarget;
        private LayoutObserver? _targetLayoutObserver;
        private UIElement?      _observedContent;
        private LayoutObserver? _contentLayoutObserver;
        private Rect            _basePlacementRectangle;
        private Rect            _appliedPlacementRectangle;
        private bool            _hasAppliedPlacementRectangle;
        private bool            _isApplyingPlacementRectangle;

        public PlacementRectangleController(DependencyObject owner)
        {
            _owner       = owner;
            _popup       = owner as Popup;
            _flyout      = owner as Flyout;
            _contextMenu = owner as NativeContextMenu;

            _placementRectangleProperty = _contextMenu is not null
                ? NativeContextMenu.PlacementRectangleProperty
                : Popup.PlacementRectangleProperty;

            _placementProperty = _contextMenu is not null
                ? NativeContextMenu.PlacementProperty
                : Popup.PlacementProperty;

            _placementTargetProperty = _popup is not null
                ? Popup.PlacementTargetProperty
                : _contextMenu is not null
                    ? NativeContextMenu.PlacementTargetProperty
                    : Flyout.PlacementTargetProperty;

            _basePlacementRectangle = (Rect)owner.GetValue(_placementRectangleProperty);
        }

        public bool IsGapEnabled       { get; set; }
        public bool IsAlignmentEnabled { get; set; }

        public void Attach()
        {
            if (_popup is not null)
            {
                _popup.Opened += OnPopupOpened;
            }
            else if (_contextMenu is not null)
            {
                _contextMenu.Opened += OnPopupOpened;
            }

            // Binding directly to dependency properties uses WPF's weak dependency-source notifications.
            // A cached DependencyPropertyDescriptor would retain the owner even if this popup never opens.
            Observe(_placementRectangleProperty, OnPlacementRectangleChanged);
            Observe(_placementTargetProperty, OnPlacementTargetChanged);
            Observe(_placementProperty, OnPlacementChanged);

            if (_popup is not null)
            {
                Observe(Popup.ChildProperty, OnPopupChildChanged);
            }

            Observe(FrameworkElement.HorizontalAlignmentProperty, OnAlignmentChanged);
            Observe(FrameworkElement.VerticalAlignmentProperty, OnAlignmentChanged);

            RefreshObservedTarget();
            RefreshObservedContent();
            UpdatePlacementRectangle();
        }

        public void Detach()
        {
            if (_popup is not null)
            {
                _popup.Opened -= OnPopupOpened;
            }
            else if (_contextMenu is not null)
            {
                _contextMenu.Opened -= OnPopupOpened;
            }

            foreach (var observer in _propertyObservers)
            {
                observer.Detach();
            }

            _propertyObservers.Clear();

            SetObservedTarget(null);
            SetObservedContent(null);
            RestoreBasePlacementRectangle();
        }

        private void Observe(DependencyProperty property, EventHandler callback)
        {
            _propertyObservers.Add(new PropertyObserver(_owner, property, callback));
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            // An implicit target or a template-bound explicit target may only be available when opening begins.
            // Refreshing here still precedes the deferred placement pass caused by changing PlacementRectangle.
            RefreshObservedTarget();
            RefreshObservedContent();
            UpdatePlacementRectangle();
        }

        private void OnPlacementRectangleChanged(object? sender, EventArgs e)
        {
            if (_isApplyingPlacementRectangle)
            {
                return;
            }

            // Treat a binding, style or direct assignment as a new unmodified contract. Reapply all active placement
            // contributions immediately so WPF never performs a later pass with stale geometry.
            _basePlacementRectangle = (Rect)_owner.GetValue(_placementRectangleProperty);
            UpdatePlacementRectangle();
        }

        private void OnPlacementTargetChanged(object? sender, EventArgs e)
        {
            RefreshObservedTarget();
            UpdatePlacementRectangle();
        }

        private void OnPlacementChanged(object? sender, EventArgs e)
        {
            UpdatePlacementRectangle();
        }

        private void OnPopupChildChanged(object? sender, EventArgs e)
        {
            RefreshObservedContent();
            UpdatePlacementRectangle();
        }

        private void OnAlignmentChanged(object? sender, EventArgs e)
        {
            // Instance-level observation leaves FrameworkElement's metadata available to application-defined Popup
            // subclasses and composes with any metadata callbacks that those subclasses install themselves.
            UpdatePlacementRectangle();
        }

        private void OnObservedLayoutUpdated()
        {
            // An implicit target rectangle and every alignment contribution depend on current render sizes. Equality
            // checks below keep this global layout notification cheap when neither target nor content size changed.
            UpdatePlacementRectangle();
        }

        private void RefreshObservedTarget()
        {
            SetObservedTarget(
                _popup?.PlacementTarget
                ?? _flyout?.PlacementTarget
                ?? _contextMenu?.PlacementTarget
                ?? (_popup is not null ? VisualTreeHelper.GetParent(_popup) as UIElement : null)
            );
        }

        private void SetObservedTarget(UIElement? target)
        {
            if (ReferenceEquals(_observedTarget, target))
            {
                return;
            }

            _targetLayoutObserver?.Detach();

            _observedTarget = target;
            _targetLayoutObserver = null;

            if (_observedTarget is not null)
            {
                _targetLayoutObserver = new LayoutObserver(_observedTarget, this);
            }
        }

        private void RefreshObservedContent()
        {
            // Flyout itself is the Child of its private Popup. Observing the surface keeps alignment synchronized after
            // its first popup-window measure as well as after template, content, width or height changes.
            SetObservedContent(_popup?.Child ?? (UIElement?)_contextMenu ?? _flyout);
        }

        private void SetObservedContent(UIElement? content)
        {
            if (ReferenceEquals(_observedContent, content))
            {
                return;
            }

            _contentLayoutObserver?.Detach();
            _observedContent = content;
            _contentLayoutObserver = content is null ? null : new LayoutObserver(content, this);
        }

        public void UpdatePlacementRectangle()
        {
            var placement = (PlacementMode)_owner.GetValue(_placementProperty);

            var appliesVerticalGap = IsGapEnabled && GetVertical(_owner) > 0D
                && (placement is PlacementMode.Top or PlacementMode.Bottom);

            var appliesHorizontalGap = IsGapEnabled && GetHorizontal(_owner) > 0D
                && (placement is PlacementMode.Left or PlacementMode.Right);

            var horizontalAlignment = (HorizontalAlignment)_owner.GetValue(
                FrameworkElement.HorizontalAlignmentProperty
            );

            var verticalAlignment = (VerticalAlignment)_owner.GetValue(
                FrameworkElement.VerticalAlignmentProperty
            );

            var appliesHorizontalAlignment = IsAlignmentEnabled
                && horizontalAlignment != HorizontalAlignment.Stretch
                && (placement is PlacementMode.Top or PlacementMode.Bottom or PlacementMode.Relative or PlacementMode.Center);

            var appliesVerticalAlignment = IsAlignmentEnabled
                && verticalAlignment != VerticalAlignment.Stretch
                && (placement is PlacementMode.Left or PlacementMode.Right or PlacementMode.Relative or PlacementMode.Center);

            if (!appliesVerticalGap && !appliesHorizontalGap
                && !appliesHorizontalAlignment && !appliesVerticalAlignment)
            {
                RestoreBasePlacementRectangle();
                return;
            }

            var observedTarget = _observedTarget;
            if (_basePlacementRectangle == Rect.Empty && observedTarget is null)
            {
                RestoreBasePlacementRectangle();
                return;
            }

            var effectiveRectangle = _basePlacementRectangle == Rect.Empty
                ? new Rect(
                    0D,
                    0D,
                    observedTarget!.RenderSize.Width,
                    observedTarget.RenderSize.Height
                )
                : _basePlacementRectangle;

            if (appliesVerticalGap)
            {
                var gap = GetVertical(_owner);
                effectiveRectangle.Y -= gap;
                effectiveRectangle.Height += gap * 2D;
            }

            if (appliesHorizontalGap)
            {
                var gap = GetHorizontal(_owner);
                effectiveRectangle.X -= gap;
                effectiveRectangle.Width += gap * 2D;
            }

            if (_observedContent is not null && (appliesHorizontalAlignment || appliesVerticalAlignment))
            {
                var contentSize = GetContentSizeInTargetCoordinates();
                if (appliesHorizontalAlignment)
                {
                    effectiveRectangle.X += GetAlignmentOffset(
                        effectiveRectangle.Width,
                        contentSize.Width,
                        horizontalAlignment
                    );

                    effectiveRectangle.Width = contentSize.Width;
                }

                if (appliesVerticalAlignment)
                {
                    effectiveRectangle.Y += GetAlignmentOffset(
                        effectiveRectangle.Height,
                        contentSize.Height,
                        verticalAlignment
                    );

                    effectiveRectangle.Height = contentSize.Height;
                }
            }

            if (_hasAppliedPlacementRectangle && _appliedPlacementRectangle == effectiveRectangle)
            {
                return;
            }

            SetPlacementRectangle(effectiveRectangle);

            _appliedPlacementRectangle    = effectiveRectangle;
            _hasAppliedPlacementRectangle = true;
        }

        /// <summary>
        /// Converts popup-content extents into the coordinate system consumed by
        /// PlacementRectangle.
        /// </summary>
        private Size GetContentSizeInTargetCoordinates()
        {
            var content = _observedContent!;
            var size    = content.RenderSize;
            var target  = _observedTarget;

            if (target is null
                || PresentationSource.FromVisual(target) is null
                || PresentationSource.FromVisual(content) is null)
            {
                // Before the popup HWND exists there is no shared coordinate space. Opening and layout notifications
                // replace this provisional size once WPF has applied the popup's actual transforms and monitor DPI.
                return size;
            }

            try
            {
                // Popup.GetChildInterestPoints and GetPlacementTargetInterestPoints transform the two surfaces
                // independently. In particular, a standalone popup does not inherit the target's LayoutTransform.
                // Screen coordinates bridge their HWNDs; subtracting the origin removes placement/animation offsets
                // so moving the popup cannot feed its position back into the alignment calculation.
                var origin = target.PointFromScreen(content.PointToScreen(default));
                var x      = target.PointFromScreen(content.PointToScreen(new Point(size.Width, 0D))) - origin;
                var y      = target.PointFromScreen(content.PointToScreen(new Point(0D, size.Height))) - origin;

                var width  = Math.Abs(x.X) + Math.Abs(y.X);
                var height = Math.Abs(x.Y) + Math.Abs(y.Y);

                if (!double.IsNaN(width) && !double.IsInfinity(width)
                    && !double.IsNaN(height) && !double.IsInfinity(height))
                {
                    return new Size(width, height);
                }
            }
            catch (InvalidOperationException)
            {
                // A disconnected visual or a singular target transform has no inverse screen mapping. Keep the
                // provisional local size; a later valid layout recomputes it without interrupting popup lifecycle.
            }

            return size;
        }

        private static double GetAlignmentOffset(
            double targetLength,
            double popupLength,
            HorizontalAlignment alignment)
        {
            return alignment switch
            {
                HorizontalAlignment.Center => (targetLength - popupLength) / 2D,
                HorizontalAlignment.Right  => targetLength - popupLength,
                _                          => 0D,
            };
        }

        private static double GetAlignmentOffset(
            double targetLength,
            double popupLength,
            VerticalAlignment alignment)
        {
            return alignment switch
            {
                VerticalAlignment.Center => (targetLength - popupLength) / 2D,
                VerticalAlignment.Bottom => targetLength - popupLength,
                _                        => 0D,
            };
        }

        private void RestoreBasePlacementRectangle()
        {
            if (!_hasAppliedPlacementRectangle)
            {
                return;
            }

            SetPlacementRectangle(_basePlacementRectangle);
            _hasAppliedPlacementRectangle = false;
        }

        private void SetPlacementRectangle(Rect value)
        {
            _isApplyingPlacementRectangle = true;

            try
            {
                // SetCurrentValue keeps a consumer's binding expression installed. A later source update is observed
                // as a new base rectangle and expanded again rather than being silently disconnected by the behavior.
                _owner.SetCurrentValue(_placementRectangleProperty, value);
            }
            finally
            {
                _isApplyingPlacementRectangle = false;
            }
        }

        /// <summary>
        /// Receives dependency-property changes without registering the owner in a global
        /// descriptor cache.
        /// </summary>
        /// <remarks>
        /// The controller owns these binding targets. WPF holds their dependency links weakly, so
        /// the entire
        /// controller/owner graph can be collected even for an unopened popup. An explicit DP path
        /// avoids CLR
        /// PropertyDescriptor subscriptions. Detaching clears the binding before releasing the
        /// callback.
        /// </remarks>
        private sealed class PropertyObserver : DependencyObject
        {
            private static readonly DependencyProperty ValueProperty
                = DependencyProperty.Register(
                    "Value",
                    typeof(object),
                    typeof(PropertyObserver),
                    new PropertyMetadata(null, OnValueChanged)
                );

            private EventHandler? _callback;

            /// <summary>
            /// Initializes a new instance of the <see cref="PropertyObserver"/> class.
            /// Establishes a one-way DP observation without notifying the controller during the
            /// initial transfer.
            /// </summary>
            /// <param name="source">The popup surface whose dependency property is
            /// observed.</param>
            /// <param name="property">The dependency property, supplied directly to avoid CLR
            /// property lookup.</param>
            /// <param name="callback">Receives subsequent effective-value changes on the owning
            /// dispatcher.</param>
            public PropertyObserver(DependencyObject source, DependencyProperty property, EventHandler callback)
            {
                // Initial transfer establishes the baseline; it must not call into a partially attached controller.
                BindingOperations.SetBinding(this, ValueProperty, new Binding
                {
                    Source = source,
                    Path   = new PropertyPath(property),
                    Mode   = BindingMode.OneWay,
                });

                _callback = callback;
            }

            /// <summary>
            /// Stops observation without invoking the controller while the binding value is
            /// cleared.
            /// </summary>
            public void Detach()
            {
                _callback = null;
                BindingOperations.ClearBinding(this, ValueProperty);
            }

            private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var observer = (PropertyObserver)d;
                observer._callback?.Invoke(observer, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Observes target or popup-content layout without letting that element retain a
        /// resource-owned Popup.
        /// </summary>
        /// <remarks>
        /// UIElement.LayoutUpdated does not have a dedicated public weak-event manager. The
        /// observer is strongly held
        /// by the target but keeps only a weak reference to the placement controller; it removes
        /// itself when either
        /// the controller explicitly detaches or a later layout discovers that the controller was
        /// collected.
        /// </remarks>
        private sealed class LayoutObserver
        {
            private readonly WeakReference<PlacementRectangleController> _controller;
            private UIElement? _target;

            public LayoutObserver(UIElement target, PlacementRectangleController controller)
            {
                _target     = target;
                _controller = new WeakReference<PlacementRectangleController>(controller);

                target.LayoutUpdated += OnLayoutUpdated;
            }

            public void Detach()
            {
                if (_target is null)
                {
                    return;
                }

                _target.LayoutUpdated -= OnLayoutUpdated;
                _target = null;
            }

            private void OnLayoutUpdated(object? sender, EventArgs e)
            {
                if (_controller.TryGetTarget(out var controller))
                {
                    controller.OnObservedLayoutUpdated();
                    return;
                }

                Detach();
            }
        }
    }
}
