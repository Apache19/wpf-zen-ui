// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Wpf.Ui.Controls;

namespace Wpf.Ui.AutomationPeers;

/// <summary>
/// Exposes <see cref="PassiveScrollViewer"/> scrolling to UI Automation while honoring its
/// horizontal visibility threshold.
/// </summary>
/// <remarks>
/// WPF's native <see cref="ScrollViewerAutomationPeer"/> considers every positive difference
/// between extent and viewport scrollable. <see cref="PassiveScrollViewer"/> deliberately treats a
/// configured sub-threshold range as rounding noise and commits its offset to zero, so reporting
/// that raw range would offer automation clients an operation that can never take effect. This peer
/// keeps vertical behavior native and changes only the suppressed horizontal range.
/// </remarks>
public sealed class PassiveScrollViewerAutomationPeer : ScrollViewerAutomationPeer, IScrollProvider
{
    private bool _lastHorizontallyScrollable;
    private double _lastHorizontalScrollPercent;
    private double _lastHorizontalViewSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassiveScrollViewerAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The viewer represented by this automation peer.</param>
    public PassiveScrollViewerAutomationPeer(PassiveScrollViewer owner)
        : base(owner)
    {
        SynchronizeHorizontalScrollState();
    }

    private PassiveScrollViewer ScrollOwner => (PassiveScrollViewer)Owner;

    bool IScrollProvider.HorizontallyScrollable => IsHorizontallyScrollable;

    double IScrollProvider.HorizontalScrollPercent => HorizontalScrollPercent;

    double IScrollProvider.HorizontalViewSize => HorizontalViewSize;

    bool IScrollProvider.VerticallyScrollable => IsVerticallyScrollable;

    double IScrollProvider.VerticalScrollPercent => VerticalScrollPercent;

    double IScrollProvider.VerticalViewSize => VerticalViewSize;

    void IScrollProvider.Scroll(ScrollAmount horizontalAmount, ScrollAmount verticalAmount)
    {
        EnsureEnabled();

        if (
            (horizontalAmount != ScrollAmount.NoAmount && !IsHorizontallyScrollable)
            || (verticalAmount != ScrollAmount.NoAmount && !IsVerticallyScrollable)
        )
        {
            throw new InvalidOperationException("The requested direction cannot be scrolled.");
        }

        switch (horizontalAmount)
        {
            case ScrollAmount.LargeDecrement:
                ScrollOwner.PageLeft();
                break;
            case ScrollAmount.SmallDecrement:
                ScrollOwner.LineLeft();
                break;
            case ScrollAmount.SmallIncrement:
                ScrollOwner.LineRight();
                break;
            case ScrollAmount.LargeIncrement:
                ScrollOwner.PageRight();
                break;
            case ScrollAmount.NoAmount:
                break;
            default:
                throw new InvalidOperationException("The requested scroll amount is invalid.");
        }

        switch (verticalAmount)
        {
            case ScrollAmount.LargeDecrement:
                ScrollOwner.PageUp();
                break;
            case ScrollAmount.SmallDecrement:
                ScrollOwner.LineUp();
                break;
            case ScrollAmount.SmallIncrement:
                ScrollOwner.LineDown();
                break;
            case ScrollAmount.LargeIncrement:
                ScrollOwner.PageDown();
                break;
            case ScrollAmount.NoAmount:
                break;
            default:
                throw new InvalidOperationException("The requested scroll amount is invalid.");
        }
    }

    void IScrollProvider.SetScrollPercent(double horizontalPercent, double verticalPercent)
    {
        EnsureEnabled();

        bool scrollHorizontally = horizontalPercent != ScrollPatternIdentifiers.NoScroll;
        bool scrollVertically = verticalPercent != ScrollPatternIdentifiers.NoScroll;
        if (
            (scrollHorizontally && !IsHorizontallyScrollable)
            || (scrollVertically && !IsVerticallyScrollable)
        )
        {
            throw new InvalidOperationException("The requested direction cannot be scrolled.");
        }

        ValidateScrollPercent(horizontalPercent, scrollHorizontally, nameof(horizontalPercent));
        ValidateScrollPercent(verticalPercent, scrollVertically, nameof(verticalPercent));

        if (scrollHorizontally)
        {
            ScrollOwner.ScrollToHorizontalOffset(ScrollOwner.ScrollableWidth * horizontalPercent * 0.01);
        }

        if (scrollVertically)
        {
            ScrollOwner.ScrollToVerticalOffset(ScrollOwner.ScrollableHeight * verticalPercent * 0.01);
        }
    }

    /// <summary>
    /// Raises threshold-dependent UI Automation property changes when the configured suppression
    /// policy changes without an accompanying WPF scroll event.
    /// </summary>
    internal void RaiseHorizontalScrollSemanticsChanged()
    {
        bool horizontallyScrollable = IsHorizontallyScrollable;
        double horizontalScrollPercent = HorizontalScrollPercent;
        double horizontalViewSize = HorizontalViewSize;

        if (_lastHorizontallyScrollable != horizontallyScrollable)
        {
            RaisePropertyChangedEvent(
                ScrollPatternIdentifiers.HorizontallyScrollableProperty,
                _lastHorizontallyScrollable,
                horizontallyScrollable
            );
        }

        if (_lastHorizontalScrollPercent != horizontalScrollPercent)
        {
            RaisePropertyChangedEvent(
                ScrollPatternIdentifiers.HorizontalScrollPercentProperty,
                _lastHorizontalScrollPercent,
                horizontalScrollPercent
            );
        }

        if (_lastHorizontalViewSize != horizontalViewSize)
        {
            RaisePropertyChangedEvent(
                ScrollPatternIdentifiers.HorizontalViewSizeProperty,
                _lastHorizontalViewSize,
                horizontalViewSize
            );
        }

        SynchronizeHorizontalScrollState();
    }

    /// <summary>
    /// Synchronizes the cached values after WPF has raised its ordinary scroll automation events.
    /// </summary>
    internal void SynchronizeHorizontalScrollState()
    {
        _lastHorizontallyScrollable = IsHorizontallyScrollable;
        _lastHorizontalScrollPercent = HorizontalScrollPercent;
        _lastHorizontalViewSize = HorizontalViewSize;
    }

    private bool IsHorizontallyScrollable =>
        ScrollOwner.HasScrollInfo
        && ScrollOwner.ScrollableWidth > 0.0
        && !ScrollOwner.IsHorizontalScrollRangeSuppressed;

    private bool IsVerticallyScrollable =>
        ScrollOwner.HasScrollInfo && ScrollOwner.ScrollableHeight > 0.0;

    private double HorizontalScrollPercent =>
        IsHorizontallyScrollable
            ? ScrollOwner.HorizontalOffset * 100.0 / ScrollOwner.ScrollableWidth
            : ScrollPatternIdentifiers.NoScroll;

    private double VerticalScrollPercent =>
        IsVerticallyScrollable
            ? ScrollOwner.VerticalOffset * 100.0 / ScrollOwner.ScrollableHeight
            : ScrollPatternIdentifiers.NoScroll;

    private double HorizontalViewSize =>
        !IsHorizontallyScrollable || ScrollOwner.ExtentWidth == 0.0
            ? 100.0
            : Math.Min(100.0, ScrollOwner.ViewportWidth * 100.0 / ScrollOwner.ExtentWidth);

    private double VerticalViewSize =>
        !ScrollOwner.HasScrollInfo || ScrollOwner.ExtentHeight == 0.0
            ? 100.0
            : Math.Min(100.0, ScrollOwner.ViewportHeight * 100.0 / ScrollOwner.ExtentHeight);

    private void EnsureEnabled()
    {
        if (!ScrollOwner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }
    }

    private static void ValidateScrollPercent(double value, bool isRequested, string parameterName)
    {
        if (isRequested && (value < 0.0 || value > 100.0 || double.IsNaN(value)))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
