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
