// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Adds direction-aware spacing between a <see cref="Popup"/> and its placement target.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Popup.VerticalOffset"/> always translates the placement rectangle downward in screen coordinates. It
/// therefore creates a gap for a Bottom candidate but an overlap for the Top candidate selected by WPF near a monitor
/// edge. Changing the offset after observing that selected side feeds a new value back into WPF's candidate scoring
/// and can make the Popup alternate indefinitely between Top and Bottom.
/// </para>
/// <para>
/// This behavior represents vertical spacing by expanding <see cref="Popup.PlacementRectangle"/> equally at its top
/// and bottom. Both candidates are consequently scored with the same gap in one positioning pass, so the selected
/// side does not alter any of its own inputs. The expansion applies to <see cref="PlacementMode.Top"/> and
/// <see cref="PlacementMode.Bottom"/>; other modes retain their original placement rectangle. A caller can still use
/// <see cref="Popup.VerticalOffset"/> independently when a direction-independent screen translation is intended.
/// </para>
/// </remarks>
public static class PopupPlacementGap
{
    #pragma warning disable WPF0061 // Accessor method for attached property should have standard documentation text

    /// <summary>
    /// Identifies the attached property that specifies equal spacing above and below a Popup's placement target.
    /// </summary>
    /// <remarks>
    /// The default is zero. Values must be finite and non-negative. The value is expressed in device-independent
    /// units and participates in WPF's normal placement scoring through an expanded placement rectangle; it does not
    /// affect the target's layout. Setting the property on a <see cref="Flyout"/> is supported; the resulting expanded
    /// placement rectangle flows through Flyout's normal binding to its internal Popup.
    /// </remarks>
    public static readonly DependencyProperty VerticalProperty =
        DependencyProperty.RegisterAttached(
            "Vertical",
            typeof(double),
            typeof(PopupPlacementGap),
            new PropertyMetadata(0D, OnVerticalChanged),
            ValidateVertical
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

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(PlacementGapController),
            typeof(PopupPlacementGap),
            new PropertyMetadata(null)
        );

    #pragma warning restore WPF0061 // Accessor method for attached property should have standard documentation text

    private static bool ValidateVertical(object? value)
    {
        return value is double gap && gap >= 0D && !double.IsInfinity(gap);
    }

    private static void OnVerticalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not Flyout)
        {
            // Other DependencyObjects retain the value without side effects, which is consistent with normal
            // attached-property behavior.
            return;
        }

        if ((double)e.NewValue <= 0D)
        {
            if (d.GetValue(ControllerProperty) is PlacementGapController previousController)
            {
                previousController.Detach();
                d.ClearValue(ControllerProperty);
            }

            return;
        }

        if (d.GetValue(ControllerProperty) is PlacementGapController controller)
        {
            controller.UpdatePlacementRectangle();
            return;
        }

        controller = new PlacementGapController(d);
        d.SetCurrentValue(ControllerProperty, controller);
        controller.Attach();
    }

    /// <summary>
    /// Maintains an expanded placement rectangle while preserving the caller's unexpanded value for restoration.
    /// </summary>
    private sealed class PlacementGapController
    {
        private readonly DependencyObject             _owner;
        private readonly Popup?                       _popup;
        private readonly Flyout?                      _flyout;
        private readonly DependencyPropertyDescriptor _placementRectangleDescriptor;
        private readonly DependencyPropertyDescriptor _placementTargetDescriptor;
        private readonly DependencyPropertyDescriptor _placementDescriptor;

        private UIElement?            _observedTarget;
        private TargetLayoutObserver? _targetLayoutObserver;
        private Rect                  _basePlacementRectangle;
        private Rect                  _appliedPlacementRectangle;
        private bool                  _hasAppliedPlacementRectangle;
        private bool                  _isApplyingPlacementRectangle;

        public PlacementGapController(DependencyObject owner)
        {
            _owner  = owner;
            _popup  = owner as Popup;
            _flyout = owner as Flyout;

            var ownerType = owner.GetType();
            _placementRectangleDescriptor = DependencyPropertyDescriptor.FromProperty(
                Popup.PlacementRectangleProperty,
                ownerType
            );

            _placementTargetDescriptor = DependencyPropertyDescriptor.FromProperty(
                _popup is not null ? Popup.PlacementTargetProperty : Flyout.PlacementTargetProperty,
                ownerType
            );

            _placementDescriptor = DependencyPropertyDescriptor.FromProperty(Popup.PlacementProperty, ownerType);

            _basePlacementRectangle = (Rect)owner.GetValue(Popup.PlacementRectangleProperty);
        }

        public void Attach()
        {
            if (_popup is not null)
            {
                _popup.Opened += OnPopupOpened;
            }

            _placementRectangleDescriptor.AddValueChanged(_owner, OnPlacementRectangleChanged);
            _placementTargetDescriptor.AddValueChanged(_owner, OnPlacementTargetChanged);
            _placementDescriptor.AddValueChanged(_owner, OnPlacementChanged);

            RefreshObservedTarget();
            UpdatePlacementRectangle();
        }

        public void Detach()
        {
            if (_popup is not null)
            {
                _popup.Opened -= OnPopupOpened;
            }

            _placementRectangleDescriptor.RemoveValueChanged(_owner, OnPlacementRectangleChanged);
            _placementTargetDescriptor.RemoveValueChanged(_owner, OnPlacementTargetChanged);
            _placementDescriptor.RemoveValueChanged(_owner, OnPlacementChanged);

            SetObservedTarget(null);
            RestoreBasePlacementRectangle();
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            // An implicit target or a template-bound explicit target may only be available when opening begins.
            // Refreshing here still precedes the deferred placement pass caused by changing PlacementRectangle.
            RefreshObservedTarget();
            UpdatePlacementRectangle();
        }

        private void OnPlacementRectangleChanged(object? sender, EventArgs e)
        {
            if (_isApplyingPlacementRectangle)
            {
                return;
            }

            // Treat a binding, style or direct assignment as a new unexpanded contract. Reapply the gap immediately
            // so WPF never performs a later layout pass with a one-sided or stale anchor rectangle.
            _basePlacementRectangle = (Rect)_owner.GetValue(Popup.PlacementRectangleProperty);
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

        private void OnTargetLayoutUpdated()
        {
            // Rect.Empty means "use the target's render bounds". Once expanded, the effective property is concrete,
            // so keep it synchronized explicitly after target layout. Equality checks below make the global layout
            // notification cheap when the target's size did not change.
            if (_basePlacementRectangle == Rect.Empty)
            {
                UpdatePlacementRectangle();
            }
        }

        private void RefreshObservedTarget()
        {
            SetObservedTarget(
                _popup?.PlacementTarget
                ?? _flyout?.PlacementTarget
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
                _targetLayoutObserver = new TargetLayoutObserver(_observedTarget, this);
            }
        }

        public void UpdatePlacementRectangle()
        {
            var gap = GetVertical(_owner);
            var placement = (PlacementMode)_owner.GetValue(Popup.PlacementProperty);
            if (gap <= 0D || placement is not (PlacementMode.Top or PlacementMode.Bottom))
            {
                RestoreBasePlacementRectangle();
                return;
            }

            Rect expandedRectangle;
            if (_basePlacementRectangle == Rect.Empty)
            {
                if (_observedTarget is null)
                {
                    return;
                }

                expandedRectangle = new Rect(
                    0D,
                    -gap,
                    _observedTarget.RenderSize.Width,
                    _observedTarget.RenderSize.Height + (gap * 2D)
                );
            }
            else
            {
                expandedRectangle = new Rect(
                    _basePlacementRectangle.X,
                    _basePlacementRectangle.Y - gap,
                    _basePlacementRectangle.Width,
                    _basePlacementRectangle.Height + (gap * 2D)
                );
            }

            if (_hasAppliedPlacementRectangle && _appliedPlacementRectangle == expandedRectangle)
            {
                return;
            }

            SetPlacementRectangle(expandedRectangle);
            _appliedPlacementRectangle    = expandedRectangle;
            _hasAppliedPlacementRectangle = true;
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
                _owner.SetCurrentValue(Popup.PlacementRectangleProperty, value);
            }
            finally
            {
                _isApplyingPlacementRectangle = false;
            }
        }

        /// <summary>
        /// Observes target layout without letting a long-lived target retain a resource-owned Popup.
        /// </summary>
        /// <remarks>
        /// UIElement.LayoutUpdated does not have a dedicated public weak-event manager. The observer is strongly held
        /// by the target but keeps only a weak reference to the placement controller; it removes itself when either
        /// the controller explicitly detaches or a later layout discovers that the controller was collected.
        /// </remarks>
        private sealed class TargetLayoutObserver
        {
            private readonly WeakReference<PlacementGapController> _controller;
            private UIElement? _target;

            public TargetLayoutObserver(UIElement target, PlacementGapController controller)
            {
                _target     = target;
                _controller = new WeakReference<PlacementGapController>(controller);
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
                    controller.OnTargetLayoutUpdated();
                    return;
                }

                Detach();
            }
        }
    }
}
