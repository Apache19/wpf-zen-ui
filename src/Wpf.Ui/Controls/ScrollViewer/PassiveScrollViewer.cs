// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A custom ScrollViewer that allows certain mouse events to bubble through when it's inactive.
/// </summary>
public class PassiveScrollViewer : ScrollViewer
{
    /// <summary>
    /// Identifies the <see cref="ScrollSpillMode"/> attached dependency property.
    /// </summary>
    /// <remarks>
    /// The property is attached so a control can configure the scrolling host hidden inside its
    /// template. Templates must forward the value explicitly; it deliberately does not inherit
    /// through the element tree because nested scrolling surfaces can require different policies.
    /// </remarks>
    public static readonly DependencyProperty ScrollSpillModeProperty =
        DependencyProperty.RegisterAttached(
            nameof(ScrollSpillMode),
            typeof(ScrollSpillMode),
            typeof(PassiveScrollViewer),
            new FrameworkPropertyMetadata(ScrollSpillMode.Enabled)
        );

    /// <summary>
    /// Sets how the <see cref="PassiveScrollViewer"/> created by an element's control template
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
    public static ScrollSpillMode GetScrollSpillMode(DependencyObject element)
    {
        return (ScrollSpillMode)element.GetValue(ScrollSpillModeProperty);
    }

    /// <summary>
    /// Gets or sets how mouse wheel events are handled when the content cannot scroll further.
    /// </summary>
    public ScrollSpillMode ScrollSpillMode
    {
        get => GetScrollSpillMode(this);
        set => SetScrollSpillMode(this, value);
    }

    static PassiveScrollViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PassiveScrollViewer),
            new FrameworkPropertyMetadata(typeof(PassiveScrollViewer))
        );
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (ScrollSpillMode == ScrollSpillMode.Native)
        {
            base.OnMouseWheel(e);
            return;
        }

        // We only care about one question: can the content actually scroll?
        // Either it's too small to overflow, or we've already hit the end.
        // ScrollBar visibility is the native control's own business — not ours.
        if (IsContentSmallerThanViewport || HasReachedEndOfScrolling(e))
        {
            e.Handled = ScrollSpillMode == ScrollSpillMode.Disabled;
            return;
        }

        base.OnMouseWheel(e);
    }

    /// <summary>
    /// Responds to changes in the scrollable extent, viewport, or offsets.
    /// </summary>
    /// <param name="e">Data describing the scrolling-state change.</param>
    /// <remarks>
    /// When a shrinking content area reduces the vertical scroll range, the coerced vertical offset
    /// is committed permanently instead of being used only for the current layout pass. This prevents
    /// a previously requested, now out-of-range offset from returning if the content grows again.
    /// </remarks>
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        // ScrollContentPresenter keeps the requested offset separately from the computed offset that
        // ScrollViewer exposes. When the extent shrinks, WPF coerces only the computed value into the
        // new range and can restore the stale request when the extent grows again. A simultaneous
        // extent/viewport change and vertical movement identifies a layout adjustment rather than an
        // isolated user scroll. Recover the offset from before that adjustment and clamp it to the new
        // range. This both rejects anchoring shifts from growing content and commits a valid replacement
        // for an offset invalidated by shrinking content, including zero when content no longer overflows.
        // Use the public command queue so custom IScrollInfo implementations retain their normal contract.
        if (
            e.VerticalChange != 0d
         && (e.ExtentHeightChange != 0d || e.ViewportHeightChange != 0d)
        )
        {
            double offsetBeforeLayoutChange = e.VerticalOffset - e.VerticalChange;
            ScrollToVerticalOffset(Math.Min(Math.Max(offsetBeforeLayoutChange, 0d), ScrollableHeight));
        }

        base.OnScrollChanged(e);
    }

    private bool IsContentSmallerThanViewport
    {
        get => ScrollableHeight <= 0;
    }

    private bool HasReachedEndOfScrolling(MouseWheelEventArgs e)
    {
        var isScrollingUp      = e.Delta > 0;
        var isScrollingDown    = e.Delta < 0;
        var isTopOfViewport    = VerticalOffset == 0;
        var isBottomOfViewport = VerticalOffset >= ScrollableHeight;

        return (isScrollingUp && isTopOfViewport) || (isScrollingDown && isBottomOfViewport);
    }
}
