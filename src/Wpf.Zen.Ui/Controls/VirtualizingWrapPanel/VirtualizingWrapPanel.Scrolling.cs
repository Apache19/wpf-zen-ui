// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Contains the logical-row and natural-pixel scrolling implementation.</summary>
public partial class VirtualizingWrapPanel
{
    /// <inheritdoc />
    public bool CanHorizontallyScroll
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    public bool CanVerticallyScroll
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    public double ExtentWidth => _scrollData.ExtentWidth;

    /// <inheritdoc />
    public double ExtentHeight => _scrollData.ExtentHeight;

    /// <inheritdoc />
    public double ViewportWidth => _scrollData.ViewportWidth;

    /// <inheritdoc />
    public double ViewportHeight => _scrollData.ViewportHeight;

    /// <inheritdoc />
    public double HorizontalOffset => _scrollData.HorizontalOffset;

    /// <inheritdoc />
    public double VerticalOffset => _scrollData.VerticalOffset;

    /// <inheritdoc />
    public ScrollViewer? ScrollOwner
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    public void LineUp() => SetVerticalOffset(VerticalOffset - GetVerticalLineSize());

    /// <inheritdoc />
    public void LineDown() => SetVerticalOffset(VerticalOffset + GetVerticalLineSize());

    /// <inheritdoc />
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - NaturalScrollLine);

    /// <inheritdoc />
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + NaturalScrollLine);

    /// <inheritdoc />
    public void MouseWheelUp()
    {
        if (SystemParameters.WheelScrollLines > 0)
        {
            SetVerticalOffset(
                VerticalOffset - (GetVerticalLineSize() * SystemParameters.WheelScrollLines)
            );
        }
        else
        {
            PageUp();
        }
    }

    /// <inheritdoc />
    public void MouseWheelDown()
    {
        if (SystemParameters.WheelScrollLines > 0)
        {
            SetVerticalOffset(
                VerticalOffset + (GetVerticalLineSize() * SystemParameters.WheelScrollLines)
            );
        }
        else
        {
            PageDown();
        }
    }

    /// <inheritdoc />
    public void MouseWheelLeft()
    {
        SetHorizontalOffset(
            HorizontalOffset - (NaturalScrollLine * HorizontalMouseWheelLines)
        );
    }

    /// <inheritdoc />
    public void MouseWheelRight()
    {
        SetHorizontalOffset(
            HorizontalOffset + (NaturalScrollLine * HorizontalMouseWheelLines)
        );
    }

    /// <inheritdoc />
    public void PageUp() => SetVerticalOffset(VerticalOffset - GetPageSize());

    /// <inheritdoc />
    public void PageDown() => SetVerticalOffset(VerticalOffset + GetPageSize());

    /// <inheritdoc />
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);

    /// <inheritdoc />
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

    /// <inheritdoc />
    public void SetHorizontalOffset(double offset)
    {
        if (double.IsNaN(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var coercedOffset = CanHorizontallyScroll
            ? CoerceOffset(offset, ExtentWidth, ViewportWidth)
            : 0D;

        if (AreClose(coercedOffset, HorizontalOffset))
        {
            return;
        }

        _scrollData = _scrollData with { HorizontalOffset = coercedOffset };

        ScrollOwner?.InvalidateScrollInfo();
        InvalidateArrange();
    }

    /// <inheritdoc />
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var coercedOffset = CoerceVerticalOffset(offset, ExtentHeight, ViewportHeight);
        if (AreClose(coercedOffset, VerticalOffset))
        {
            return;
        }

        _scrollData = _scrollData with { VerticalOffset = coercedOffset };

        ScrollOwner?.InvalidateScrollInfo();
        if (_isVirtualizing)
        {
            InvalidateMeasure();
        }
        else
        {
            InvalidateArrange();
        }
    }

    /// <inheritdoc />
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (rectangle.IsEmpty
            || visual == this
            || visual is not UIElement element
            || !IsAncestorOf(element))
        {
            return Rect.Empty;
        }

        // Transform first while layout still reflects the old offsets, then add those offsets back
        // to obtain stable content coordinates. This mirrors ScrollContentPresenter's MakeVisible
        // contract and lets the returned rectangle be calculated without waiting for another arrange.
        var contentBounds = visual.TransformToAncestor(this).TransformBounds(rectangle);
        contentBounds.X += HorizontalOffset;
        contentBounds.Y += GetPhysicalVerticalOffset();

        if (_usesUniformLayout)
        {
            var container = FindDirectChild(element);
            if (container is null)
            {
                return Rect.Empty;
            }

            var itemsOwner = ItemsControl.GetItemsOwner(this);
            var itemIndex  = itemsOwner is not null
                ? itemsOwner.ItemContainerGenerator.IndexFromContainer(container)
                : InternalChildren.IndexOf(container);

            if (itemIndex < 0)
            {
                return Rect.Empty;
            }

            var row = itemIndex / _uniformLayout.Columns;
            EnsureHorizontalBoundsVisible(contentBounds);
            EnsureUniformRowVisible(row);
        }
        else
        {
            EnsurePixelBoundsVisible(contentBounds);
        }

        Rect viewport = new(
            HorizontalOffset,
            GetPhysicalVerticalOffset(),
            ViewportWidth,
            _usesUniformLayout ? _pixelViewportHeight : ViewportHeight
        );

        contentBounds.Intersect(viewport);
        if (!contentBounds.IsEmpty)
        {
            contentBounds.X -= viewport.X;
            contentBounds.Y -= viewport.Y;
        }

        return contentBounds;
    }

    /// <inheritdoc />
    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || index >= _itemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_usesUniformLayout)
        {
            EnsureUniformRowVisible(index / _uniformLayout.Columns);
        }
    }

    private void UpdateScrollData(
        double extentWidth,
        double extentHeight,
        double viewportWidth,
        double viewportHeight,
        double requestedVerticalOffset
    )
    {
        var horizontalOffset = CanHorizontallyScroll
            ? CoerceOffset(HorizontalOffset, extentWidth, viewportWidth)
            : 0D;

        var verticalOffset = CoerceVerticalOffset(
            requestedVerticalOffset,
            extentHeight,
            viewportHeight
        );

        var updated = new ScrollData(
            extentWidth,
            extentHeight,
            viewportWidth,
            viewportHeight,
            horizontalOffset,
            verticalOffset
        );

        if (_scrollData == updated)
        {
            return;
        }

        _scrollData = updated;
        ScrollOwner?.InvalidateScrollInfo();
    }

    private void EnsureUniformRowVisible(int row)
    {
        if (_usesPixelBasedScrolling)
        {
            var rowTop = row * _uniformLayout.RowStride;
            SetVerticalOffset(
                ComputeScrollOffsetWithMinimalScroll(
                    VerticalOffset,
                    VerticalOffset + ViewportHeight,
                    rowTop,
                    rowTop + _uniformLayout.ItemHeight
                )
            );
        }
        else
        {
            var minimalOffset = ComputeScrollOffsetWithMinimalScroll(
                VerticalOffset,
                VerticalOffset + ViewportHeight,
                row,
                row + 1D
            );

            // A logical viewport can contain a fractional final row, but its leading edge must
            // remain row-aligned. Round toward the requested movement so BringIntoView reveals the
            // complete target row whenever the viewport is at least one row high.
            SetVerticalOffset(
                minimalOffset > VerticalOffset
                    ? Math.Ceiling(minimalOffset)
                    : Math.Floor(minimalOffset)
            );
        }
    }

    private void EnsurePixelBoundsVisible(Rect bounds)
    {
        EnsureHorizontalBoundsVisible(bounds);
        SetVerticalOffset(
            ComputeScrollOffsetWithMinimalScroll(
                VerticalOffset,
                VerticalOffset + ViewportHeight,
                bounds.Top,
                bounds.Bottom
            )
        );
    }

    private void EnsureHorizontalBoundsVisible(Rect bounds)
    {
        SetHorizontalOffset(
            ComputeScrollOffsetWithMinimalScroll(
                HorizontalOffset,
                HorizontalOffset + ViewportWidth,
                bounds.Left,
                bounds.Right
            )
        );
    }

    private UIElement? FindDirectChild(UIElement element)
    {
        DependencyObject current = element;
        while (VisualTreeHelper.GetParent(current) is { } parent && parent != this)
        {
            current = parent;
        }

        return VisualTreeHelper.GetParent(current) == this ? current as UIElement : null;
    }

    private double GetVerticalLineSize()
    {
        return _usesUniformLayout && !_usesPixelBasedScrolling ? 1D : NaturalScrollLine;
    }

    private double GetPageSize()
    {
        return ViewportHeight;
    }

    private double GetPhysicalVerticalOffset()
    {
        return _usesUniformLayout && !_usesPixelBasedScrolling
            ? VerticalOffset * _uniformLayout.RowStride
            : VerticalOffset;
    }

    private double CoerceVerticalOffset(double offset, double extent, double viewport)
    {
        if (!CanVerticallyScroll)
        {
            return 0D;
        }

        var coercedOffset = CoerceOffset(offset, extent, viewport);
        if (_usesUniformLayout && !_usesPixelBasedScrolling)
        {
            // ScrollViewer can send fractional values while dragging its thumb or processing
            // manipulation input. WPF's VirtualizingStackPanel treats item offsets as effectively
            // integral for the same reason: retaining the fraction would expose a clipped leading
            // item and make item scrolling visually indistinguishable from pixel scrolling. The
            // terminal offset is the necessary exception. If a viewport holds (for example) 2.5
            // rows, preserving Extent - Viewport lets the last row become fully visible without
            // introducing blank space below the content.
            var terminalOffset = Math.Max(0D, extent - viewport);
            if (!AreClose(coercedOffset, terminalOffset))
            {
                coercedOffset = Math.Floor(coercedOffset);
            }
        }

        return coercedOffset;
    }

    private static double ComputeScrollOffsetWithMinimalScroll(
        double viewportStart,
        double viewportEnd,
        double childStart,
        double childEnd
    )
    {
        var isBefore = childStart < viewportStart && childEnd < viewportEnd;
        var isAfter  = childEnd > viewportEnd && childStart > viewportStart;
        var isLarger = childEnd - childStart > viewportEnd - viewportStart;

        if ((isBefore && !isLarger) || (isAfter && isLarger))
        {
            return childStart;
        }

        if (isBefore || isAfter)
        {
            return childEnd - (viewportEnd - viewportStart);
        }

        return viewportStart;
    }

    private static double CoerceOffset(double offset, double extent, double viewport)
    {
        if (offset < 0D || viewport >= extent)
        {
            return 0D;
        }

        return Math.Min(offset, Math.Max(0D, extent - viewport));
    }

    private static bool AreClose(double left, double right)
    {
        var scale = Math.Abs(left) + Math.Abs(right) + 10D;
        return Math.Abs(left - right) < 2.2204460492503131E-16 * scale;
    }
}
