// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Zen.Ui.AutomationPeers;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides smooth pixel-based wheel, keyboard and scrollbar-button scrolling, with configurable wheel
/// propagation at boundaries.
/// </summary>
/// <remarks>
/// Uses the template's existing scroll provider; no additional named parts or transforms are required.
/// Physical content and pixel-scrolling virtualizing panels support smooth scrolling. Item-scrolling
/// providers retain their native behavior. Keyboard scrolling and a template scrollbar's line and page
/// buttons accumulate smooth motion on the applicable axis. Touch, thumb dragging and programmatic
/// scrolling retain WPF semantics and take precedence over active motion.
/// </remarks>
public partial class UiScrollViewer : ScrollViewer
{
    private static readonly DependencyProperty[] TemplatedParentAssistProperties;

    /// <summary>
    /// Identifies the <see cref="IsHorizontalMouseWheelEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsHorizontalMouseWheelEnabledProperty
        = DependencyProperty.RegisterAttached(
            nameof(IsHorizontalMouseWheelEnabled),
            typeof(bool),
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(
                false,
                OnSmoothScrollingSettingChanged
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether ordinary wheel input scrolls horizontally when vertical scrolling is
    /// unavailable and a meaningful horizontal range exists. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Reaching a vertical boundary does not select the horizontal axis. Downward wheel input
    /// increases the horizontal offset; upward input decreases it. Pixel providers use the existing
    /// smooth-scrolling settings, while item providers retain their native horizontal wheel steps.
    /// <see cref="Controls.ScrollSpillMode.Native"/> takes precedence over this setting.
    /// This property does not inherit or invalidate layout/rendering and has no coercion. It can be
    /// animated discretely; every effective change cancels active smooth motion. A template-owned
    /// viewer obtains its owner's value unless the viewer's template, style or local value overrides it.
    /// No additional template parts are required.
    /// </remarks>
    public bool IsHorizontalMouseWheelEnabled
    {
        get => GetIsHorizontalMouseWheelEnabled(this);
        set => SetIsHorizontalMouseWheelEnabled(this, value);
    }

    /// <summary>
    /// Configures horizontal wheel fallback for a viewer or its template owner.
    /// </summary>
    /// <param name="element">
    /// The viewer or template owner to configure.
    /// </param>
    /// <param name="value">
    /// Whether ordinary wheel input may select the horizontal axis.
    /// </param>
    public static void SetIsHorizontalMouseWheelEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsHorizontalMouseWheelEnabledProperty, value);
    }

    /// <summary>
    /// Gets horizontal wheel fallback for a viewer or its template owner.
    /// </summary>
    /// <param name="element">
    /// The viewer or template owner to query.
    /// </param>
    /// <returns>
    /// The configured value, or <see langword="false"/> when unset.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetIsHorizontalMouseWheelEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsHorizontalMouseWheelEnabledProperty);
    }

    /// <summary>
    /// Identifies the <see cref="ScrollSpillMode"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The property is attached so a control can configure the scrolling host hidden inside its
    /// template. A template-owned viewer forwards the owner's value unless it has its own value.
    /// It deliberately does not inherit
    /// through the element tree because nested scrolling surfaces can require different policies.
    /// </remarks>
    public static readonly DependencyProperty ScrollSpillModeProperty
        = DependencyProperty.RegisterAttached(
            nameof(ScrollSpillMode),
            typeof(ScrollSpillMode),
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(ScrollSpillMode.Enabled)
        );

    /// <summary>
    /// Gets or sets how mouse wheel events are handled when the content cannot scroll further.
    /// The default is <see cref="Controls.ScrollSpillMode.Enabled"/>.
    /// </summary>
    public ScrollSpillMode ScrollSpillMode
    {
        get => GetScrollSpillMode(this);
        set => SetScrollSpillMode(this, value);
    }

    /// <summary>
    /// Identifies the <see cref="HorizontalScrollBarVisibilityThreshold"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HorizontalScrollBarVisibilityThresholdProperty
        = DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibilityThreshold),
            typeof(double),
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(0.0),
            static value => value is double threshold
                            && !double.IsNaN(threshold)
                            && !double.IsInfinity(threshold)
                            && threshold >= 0.0
        );

    /// <summary>
    /// Gets or sets the smallest horizontal scroll range that automatic scrollbar visibility treats
    /// as meaningful. The default is <c>0</c> device-independent pixels.
    /// </summary>
    /// <remarks>
    /// A positive value suppresses rounding overflow in column controls. Explicitly visible scrollbars
    /// remain visible, and overflow larger than the threshold retains normal WPF scrolling behavior.
    /// </remarks>
    public double HorizontalScrollBarVisibilityThreshold
    {
        get => (double)GetValue(HorizontalScrollBarVisibilityThresholdProperty);
        set => SetValue(HorizontalScrollBarVisibilityThresholdProperty, value);
    }

    private static readonly DependencyPropertyKey EffectiveHorizontalScrollBarVisibilityPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(EffectiveHorizontalScrollBarVisibility),
            typeof(Visibility),
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(Visibility.Collapsed)
        );

    /// <summary>
    /// Identifies the <see cref="EffectiveHorizontalScrollBarVisibility"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EffectiveHorizontalScrollBarVisibilityProperty
        = EffectiveHorizontalScrollBarVisibilityPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the horizontal scrollbar visibility after applying
    /// <see cref="HorizontalScrollBarVisibilityThreshold"/> to WPF's computed visibility.
    /// </summary>
    public Visibility EffectiveHorizontalScrollBarVisibility
    {
        get => (Visibility)GetValue(EffectiveHorizontalScrollBarVisibilityProperty);
    }

    /// <summary>
    /// Sets how the <see cref="UiScrollViewer"/> created by an element's control template
    /// handles mouse-wheel input that cannot scroll its own content.
    /// </summary>
    /// <param name="element">The element whose scrolling policy is being configured.</param>
    /// <param name="value">The mouse-wheel spill policy forwarded by the element's template.</param>
    public static void SetScrollSpillMode(DependencyObject element, ScrollSpillMode value)
    {
        element.SetValue(ScrollSpillModeProperty, value);
    }

    /// <summary>
    /// Gets the mouse-wheel spill policy configured for an element.
    /// </summary>
    /// <param name="element">The element whose scrolling policy is being queried.</param>
    /// <returns>
    /// The configured spill policy, or <see cref="Controls.ScrollSpillMode.Enabled"/> when unset.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static ScrollSpillMode GetScrollSpillMode(DependencyObject element)
    {
        return (ScrollSpillMode)element.GetValue(ScrollSpillModeProperty);
    }

    static UiScrollViewer()
    {
        // The smooth-scrolling properties live in another partial declaration. Initialize this list
        // after all static field initializers so its contents never depend on partial-file ordering.
        TemplatedParentAssistProperties =
        [
            ScrollSpillModeProperty,
            ScrollViewerAssist.HideButtonsProperty,
            ScrollViewerAssist.OverlayModeProperty,
            IsSmoothScrollingEnabledProperty,
            SmoothScrollDurationProperty,
            IsHorizontalMouseWheelEnabledProperty,
        ];

        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(typeof(UiScrollViewer))
        );
    }

    /// <summary>
    /// Applies the control template and connects scrolling policies supplied by the control that
    /// owns this viewer in its template.
    /// </summary>
    public override void OnApplyTemplate()
    {
        StopSmoothScrolling();
        BindAssistPropertiesToTemplatedParentIfUnset();

        base.OnApplyTemplate();
    }

    /// <summary>
    /// Responds when WPF publishes a new horizontal extent, viewport, or computed scrollbar state.
    /// </summary>
    /// <param name="e">The dependency-property change data.</param>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsVisibleProperty
            || e.Property == IsEnabledProperty
            || e.Property == ContentProperty
            || e.Property == CanContentScrollProperty
            || e.Property == VerticalScrollBarVisibilityProperty
            || e.Property == ScrollSpillModeProperty)
        {
            StopSmoothScrolling();
        }

        if (e.Property == ScrollableWidthProperty
            || e.Property == ComputedHorizontalScrollBarVisibilityProperty
            || e.Property == HorizontalScrollBarVisibilityProperty
            || e.Property == HorizontalScrollBarVisibilityThresholdProperty)
        {
            UpdateEffectiveHorizontalScrollBarVisibility();

            if (e.Property == HorizontalScrollBarVisibilityProperty
                || e.Property == HorizontalScrollBarVisibilityThresholdProperty)
            {
                if (IsHorizontalScrollRangeSuppressed && HorizontalOffset != 0.0)
                {
                    // A threshold change can suppress an already active range without producing a
                    // ScrollChanged event of its own. Commit the same zero-offset invariant here.
                    ScrollToHorizontalOffset(0.0);
                }

                GetExistingAutomationPeer()?.RaiseHorizontalScrollSemanticsChanged();
            }
        }
    }

    /// <summary>
    /// Connects library scrolling properties set on the template owner to this viewer when its
    /// template, style, or local values do not already choose a value.
    /// </summary>
    private void BindAssistPropertiesToTemplatedParentIfUnset()
    {
        if (TemplatedParent == null)
        {
            return;
        }

        foreach (var property in TemplatedParentAssistProperties)
        {
            if (DependencyPropertyHelper.GetValueSource(this, property).BaseValueSource
                != BaseValueSource.Default)
            {
                continue;
            }

            // ScrollViewer.OnPreApplyTemplate conditionally binds WPF's own scrolling attached
            // properties to the templated parent. WPF's helper is internal and only handles its
            // fixed property list, so UiScrollViewer reproduces that contract for the
            // library-specific settings. A value supplied directly by the viewer or its style
            // intentionally wins. Property inheritance is not used because it would continue into
            // the owner's content and unintentionally configure independently nested viewers.
            // Pass the dependency property itself so attached-property paths do not depend on
            // resolving a XAML namespace prefix when a shared style is applied at runtime.
            BindingOperations.SetBinding(
                this,
                property,
                new Binding { Path = new PropertyPath(property), RelativeSource = RelativeSource.TemplatedParent, }
            );
        }
    }

    /// <summary>
    /// Creates the automation peer that applies the same horizontal overflow threshold exposed by
    /// the visual scrollbar template.
    /// </summary>
    /// <returns>A threshold-aware scroll provider for this viewer.</returns>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new UiScrollViewerAutomationPeer(this);
    }

    /// <summary>
    /// Selects the available wheel axis and accumulates input, or delegates to the native provider
    /// when smooth scrolling is unavailable. Boundary propagation uses the pending destination so a
    /// child does not swallow extra detents while it is still approaching its edge.
    /// </summary>
    /// <param name="e">The unhandled wheel input and its signed delta.</param>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0)
        {
            return;
        }

        if (ScrollSpillMode == ScrollSpillMode.Native)
        {
            StopSmoothScrolling();
            base.OnMouseWheel(e);
            return;
        }

        var orientation = GetMouseWheelOrientation();
        if (TrySmoothMouseWheel(e, orientation))
        {
            return;
        }

        StopSmoothScrolling();

        var maximum = orientation == Orientation.Horizontal ? ScrollableWidth : ScrollableHeight;
        var offset  = orientation == Orientation.Horizontal ? HorizontalOffset : VerticalOffset;

        if (maximum <= 0 || (e.Delta > 0 ? offset <= 0 : offset >= maximum))
        {
            e.Handled = ScrollSpillMode == ScrollSpillMode.Disabled;
            return;
        }

        if (orientation == Orientation.Horizontal)
        {
            // The provider owns pixel/item units and native step sizes. ScrollViewer's base wheel
            // handler only calls the vertical methods, so horizontal fallback must dispatch here.
            if (e.Delta < 0)
            {
                ScrollInfo.MouseWheelRight();
            }
            else
            {
                ScrollInfo.MouseWheelLeft();
            }

            e.Handled = true;
        }
        else
        {
            base.OnMouseWheel(e);
        }
    }

    /// <summary>
    /// Responds to changes in the scrollable extent, viewport, or offsets.
    /// </summary>
    /// <param name="e">Data describing the scrolling-state change.</param>
    /// <remarks>
    /// When a physical content presenter's scroll range shrinks past the current vertical offset,
    /// the coerced boundary is committed permanently instead of being used only for the current
    /// layout pass. This prevents a stale request from returning if the content grows again.
    /// Other providers retain their own offset persistence and anchoring semantics.
    /// </remarks>
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        var wasSmoothScrolling = IsSmoothScrolling;
        SynchronizeSmoothScrolling(e);

        // ScrollContentPresenter retains its requested offset when layout coerces the displayed
        // offset. Commit the new boundary after a range reduction so later growth cannot restore
        // the stale request. Other providers own their anchoring and offset persistence semantics.
        // ScrollChanged batches explicit scroll commands with size changes: their coexistence does
        // not justify restoring the previous offset. Only a move from beyond the new range to its
        // boundary requires this correction. Write directly before notifying listeners so no queued
        // correction can subsequently overwrite a listener's legitimate scroll request.
        if (!wasSmoothScrolling && ScrollInfo is ScrollContentPresenter presenter
                                && e.VerticalChange < 0d
                                && e.ExtentHeightChange - e.ViewportHeightChange < 0d
                                && e.VerticalOffset == ScrollableHeight
                                && e.VerticalOffset - e.VerticalChange > ScrollableHeight)
        {
            presenter.SetVerticalOffset(e.VerticalOffset);
        }

        // ScrollContentPresenter keeps an independent requested offset. Commit zero for a
        // sub-threshold rounding range so that range cannot reappear as a tiny visual displacement
        // after a later extent update.
        if (IsHorizontalScrollRangeSuppressed && HorizontalOffset != 0.0)
        {
            ScrollToHorizontalOffset(0.0);
        }

        base.OnScrollChanged(e);
        GetExistingAutomationPeer()?.SynchronizeHorizontalScrollState();
    }

    private void UpdateEffectiveHorizontalScrollBarVisibility()
    {
        var visibility = ComputedHorizontalScrollBarVisibility;
        if (IsHorizontalScrollRangeSuppressed)
        {
            visibility = Visibility.Collapsed;
        }

        SetValue(EffectiveHorizontalScrollBarVisibilityPropertyKey, visibility);
    }

    /// <summary>
    /// Gets a value indicating whether a positive raw horizontal range is intentionally suppressed by the configured
    /// automatic-visibility threshold.
    /// </summary>
    internal bool IsHorizontalScrollRangeSuppressed
    {
        get =>
            HorizontalScrollBarVisibility == ScrollBarVisibility.Auto
            && ScrollableWidth <= HorizontalScrollBarVisibilityThreshold;
    }

    /// <summary>
    /// Gets a value indicating whether WPF has connected this viewer to an
    /// <see cref="System.Windows.Controls.Primitives.IScrollInfo"/>
    /// implementation. The automation peer cannot access ScrollViewer's protected member directly.
    /// </summary>
    internal bool HasScrollInfo => ScrollInfo is not null;

    private UiScrollViewerAutomationPeer? GetExistingAutomationPeer()
    {
        return UIElementAutomationPeer.FromElement(this) as UiScrollViewerAutomationPeer;
    }

    /// <summary>
    /// Chooses from available ranges, never from the current offset. A vertical boundary must retain
    /// its axis so nested viewers can receive spill input instead of unexpectedly moving sideways.
    /// Hidden scrollbars still allow scrolling; only Disabled and provider capability prohibit it.
    /// </summary>
    private Orientation GetMouseWheelOrientation()
    {
        return IsHorizontalMouseWheelEnabled
               && ScrollInfo is { CanHorizontallyScroll: true } provider
               && HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
               && ScrollableWidth > 0
               && !IsHorizontalScrollRangeSuppressed
               && (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
                   || !provider.CanVerticallyScroll || ScrollableHeight <= 0)
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }
}
