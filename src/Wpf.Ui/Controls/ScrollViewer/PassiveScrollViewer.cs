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
    /// <summary>Identifies the <see cref="IsScrollSpillEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsScrollSpillEnabledProperty = DependencyProperty.Register(
        nameof(IsScrollSpillEnabled),
        typeof(bool),
        typeof(PassiveScrollViewer),
        new PropertyMetadata(true)
    );

    static PassiveScrollViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PassiveScrollViewer),
            new FrameworkPropertyMetadata(typeof(PassiveScrollViewer))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether blocked inner scrolling should be propagated forward.
    /// </summary>
    public bool IsScrollSpillEnabled
    {
        get { return (bool)GetValue(IsScrollSpillEnabledProperty); }
        set { SetValue(IsScrollSpillEnabledProperty, value); }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        // When spill is disabled, stay out of the way — let the native ScrollViewer do its thing.
        if (!IsScrollSpillEnabled)
        {
            base.OnMouseWheel(e);
            return;
        }

        // We only care about one question: can the content actually scroll?
        // Either it's too small to overflow, or we've already hit the end.
        // ScrollBar visibility is the native control's own business — not ours.
        if (IsContentSmallerThanViewport || HasReachedEndOfScrolling(e))
        {
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