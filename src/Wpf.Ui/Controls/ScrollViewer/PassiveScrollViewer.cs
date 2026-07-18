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
    /// <summary>Identifies the <see cref="ScrollSpillMode"/> dependency property.</summary>
    public static readonly DependencyProperty ScrollSpillModeProperty = DependencyProperty.Register(
        nameof(ScrollSpillMode),
        typeof(ScrollSpillMode),
        typeof(PassiveScrollViewer),
        new PropertyMetadata(ScrollSpillMode.Enabled)
    );

    /// <summary>
    /// Gets or sets how mouse wheel events are handled when the content cannot scroll further.
    /// </summary>
    public ScrollSpillMode ScrollSpillMode
    {
        get { return (ScrollSpillMode)GetValue(ScrollSpillModeProperty); }
        set { SetValue(ScrollSpillModeProperty, value); }
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
    /// When a shrinking content area removes the vertical scroll range, the vertical offset is reset
    /// permanently instead of merely being coerced to zero for the current layout pass. This prevents
    /// a previously requested offset from returning if the content later becomes scrollable again.
    /// </remarks>
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        // ScrollContentPresenter keeps the requested offset separately from the computed offset that
        // ScrollViewer exposes. When the extent shrinks, WPF only coerces the computed value into the
        // new range; it intentionally retains the requested value and restores it if the extent grows.
        // A non-zero change to zero proves that this viewer was actually scrolled before its vertical
        // range disappeared. Explicitly requesting zero updates WPF's retained value as well. The call
        // uses the public command queue so it remains compatible with custom IScrollInfo implementations.
        if (e.VerticalChange != 0d && ScrollableHeight <= 0d)
        {
            ScrollToVerticalOffset(0d);
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
