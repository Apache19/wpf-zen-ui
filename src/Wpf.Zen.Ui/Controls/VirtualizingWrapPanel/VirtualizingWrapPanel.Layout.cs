// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Contains the measurement, container-generation, and arrangement implementation.</summary>
public partial class VirtualizingWrapPanel
{
    private readonly Dictionary<double, double> _naturalRowWidths = [];

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var previouslyUsedUniformLayout       = _usesUniformLayout;
        var previouslyUsedPixelBasedScrolling = _usesPixelBasedScrolling;
        var previousUniformLayout             = _uniformLayout;
        var itemsOwner                        = ItemsControl.GetItemsOwner(this);

        _itemCount = itemsOwner?.Items.Count ?? InternalChildren.Count;
        _usesPixelBasedScrolling = itemsOwner is not null && GetScrollUnit(itemsOwner) == ScrollUnit.Pixel;

        var layoutWidth    = ResolveLayoutWidth(availableSize.Width);
        var fixedItemWidth = ResolveFixedItemWidth(
            ItemWidth,
            MinimumItemWidth,
            MaximumItemWidth,
            ItemAspectRatio,
            ItemHeight
        );

        var hasPredictableHeight = !double.IsNaN(ItemAspectRatio) || !double.IsNaN(ItemHeight);
        var hasFixedItemWidth = !double.IsNaN(fixedItemWidth);

        _usesManagedItemWidth =
            (hasFixedItemWidth || HasResponsiveWidthConstraint())
            && IsFiniteNonNegative(layoutWidth);

        _usesAutomaticItemSizing = _usesManagedItemWidth && !hasFixedItemWidth;
        _usesUniformLayout       = _usesManagedItemWidth && hasPredictableHeight;
        _pixelViewportHeight     = ResolveViewportHeight(availableSize.Height);

        if (_usesManagedItemWidth)
        {
            _uniformLayout = CalculateUniformLayout(
                layoutWidth,
                HorizontalSpacing,
                VerticalSpacing,
                fixedItemWidth,
                MinimumItemWidth,
                MaximumItemWidth,
                ItemAspectRatio,
                ItemHeight,
                _itemCount
            );
        }

        if (_usesUniformLayout)
        {
            _isVirtualizing =
                itemsOwner is not null
                && GetIsVirtualizing(itemsOwner)
                && (itemsOwner.Items.Groups is null || GetIsVirtualizingWhenGrouping(itemsOwner))
                && IsFiniteNonNegative(_pixelViewportHeight)
                && _uniformLayout.RowStride > 0D;

            PreserveVisibleItemAfterColumnChange(
                previouslyUsedUniformLayout,
                previouslyUsedPixelBasedScrolling,
                previousUniformLayout
            );

            MeasureUniformItems(itemsOwner, layoutWidth);
            PublishLayoutState(true, true, _isVirtualizing, _usesAutomaticItemSizing);

            return GetUniformDesiredSize(availableSize, layoutWidth);
        }

        _isVirtualizing = false;

        PreserveScrollPositionForNaturalLayout(
            previouslyUsedUniformLayout,
            previouslyUsedPixelBasedScrolling,
            previousUniformLayout
        );

        MeasureNaturalItems(itemsOwner, availableSize, layoutWidth);
        PublishLayoutState(_usesManagedItemWidth, false, false, _usesAutomaticItemSizing);

        return GetNaturalDesiredSize(availableSize, layoutWidth);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_usesUniformLayout)
        {
            ArrangeUniformItems();
        }
        else
        {
            ArrangeNaturalItems();
        }

        return finalSize;
    }

    /// <summary>
    /// Calculates the stable cell geometry used by both layout and virtualization.
    /// </summary>
    /// <remarks>
    /// In responsive mode, the upper bound selects the fewest columns needed to prevent unbounded
    /// growth, while the lower bound limits that result to the number of columns which can remain
    /// readable. Cells consume the available width inside the resulting interval; unused width is
    /// intentional when another minimum-width column does not yet fit. The item count caps the
    /// result so a one-row collection never manufactures empty columns as its viewport grows.
    /// </remarks>
    internal static (int Columns, double ItemWidth, double ItemHeight) CalculateLayout(
        double availableWidth,
        double horizontalSpacing,
        double itemWidth,
        double minimumItemWidth,
        double maximumItemWidth,
        double itemAspectRatio,
        double itemHeight,
        int    itemCount = int.MaxValue
    )
    {
        var result = CalculateUniformLayout(
            availableWidth,
            horizontalSpacing,
            0D,
            itemWidth,
            minimumItemWidth,
            maximumItemWidth,
            itemAspectRatio,
            itemHeight,
            itemCount
        );

        return (result.Columns, result.ItemWidth, result.ItemHeight);
    }

    private static UniformLayout CalculateUniformLayout(
        double availableWidth,
        double horizontalSpacing,
        double verticalSpacing,
        double itemWidth,
        double minimumItemWidth,
        double maximumItemWidth,
        double itemAspectRatio,
        double itemHeight,
        int    itemCount
    )
    {
        itemWidth = ResolveFixedItemWidth(
            itemWidth,
            minimumItemWidth,
            maximumItemWidth,
            itemAspectRatio,
            itemHeight
        );

        int    columns;
        double width;
        var    widthWithTrailingGap = Math.Max(0D, availableWidth) + horizontalSpacing;

        if (!double.IsNaN(itemWidth))
        {
            // A fixed item width is a hard layout request. Any remainder stays empty, and a cell
            // wider than the viewport is exposed through horizontal scrolling instead of being
            // silently shrunk by the responsive algorithm.
            columns = GetColumnCount(
                widthWithTrailingGap / (itemWidth + horizontalSpacing),
                roundUp: false
            );

            width = itemWidth;
        }
        else
        {
            var hasMinimum = minimumItemWidth > 0D;
            var hasMaximum = !double.IsInfinity(maximumItemWidth);

            // WPF's FrameworkElement gives a minimum size precedence when MinWidth and MaxWidth
            // cross. Normalizing the calculation in the same way avoids order-dependent DP
            // coercion while keeping the configured values available to bindings and styles.
            var effectiveMaximum = hasMaximum
                ? Math.Max(minimumItemWidth, maximumItemWidth)
                : double.PositiveInfinity;

            // The maximum produces the smallest useful column count (largest cells). The minimum
            // produces the largest count that can fit without narrowing cells below their content
            // constraint. Taking the smaller count preserves the panel's item-size-first policy;
            // if the two breakpoints leave a gap, cells stop at the maximum and alignment receives
            // the remainder until another minimum-width column fits.
            var columnsForMaximum = hasMaximum
                ? GetColumnCount(
                    widthWithTrailingGap / (effectiveMaximum + horizontalSpacing),
                    roundUp: true
                )
                : int.MaxValue;
            var columnsForMinimum = hasMinimum
                ? GetColumnCount(
                    widthWithTrailingGap / (minimumItemWidth + horizontalSpacing),
                    roundUp: false
                )
                : int.MaxValue;

            columns = hasMaximum && hasMinimum
                ? Math.Min(columnsForMaximum, columnsForMinimum)
                : hasMaximum
                    ? columnsForMaximum
                    : columnsForMinimum;

            // Once all items occupy one row, additional theoretical columns cannot display more
            // content. Capping here prevents repeated width drops as a sparse collection's viewport
            // crosses breakpoints for columns that would remain empty.
            columns = Math.Min(columns, Math.Max(1, itemCount));

            var totalSpacing    = horizontalSpacing * Math.Max(0, columns - 1);
            var calculatedWidth = Math.Max(0D, (availableWidth - totalSpacing) / columns);

            width = Math.Max(minimumItemWidth, Math.Min(calculatedWidth, effectiveMaximum));
        }

        var height = !double.IsNaN(itemHeight)
            ? itemHeight
            : !double.IsNaN(itemAspectRatio)
                ? width / itemAspectRatio
                : double.NaN;

        return new UniformLayout(columns, width, height, height + verticalSpacing);
    }

    private void MeasureUniformItems(ItemsControl? itemsOwner, double layoutWidth)
    {
        var totalRows         = GetRowCount(_itemCount, _uniformLayout.Columns);
        var pixelExtentHeight = GetUniformPixelExtentHeight(totalRows);

        var viewportHeight = _usesPixelBasedScrolling
            ? ResolvePixelViewportHeight(pixelExtentHeight)
            : GetUniformViewportRows(totalRows);

        var extentHeight = _usesPixelBasedScrolling ? pixelExtentHeight : totalRows;
        var extentWidth  = Math.Max(layoutWidth, _uniformLayout.ItemWidth);

        UpdateScrollData(
            extentWidth,
            extentHeight,
            layoutWidth,
            viewportHeight,
            _scrollData.VerticalOffset
        );

        var firstIndex = 0;
        var lastIndex  = _itemCount - 1;

        if (_isVirtualizing && _itemCount > 0)
        {
            var offsetRows = _usesPixelBasedScrolling
                ? _scrollData.VerticalOffset / _uniformLayout.RowStride
                : _scrollData.VerticalOffset;

            var viewportRows = (_pixelViewportHeight + VerticalSpacing) / _uniformLayout.RowStride;

            var firstVisibleRow = Math.Min(totalRows - 1, (int)Math.Floor(offsetRows));
            var lastVisibleRow  = Math.Min(
                totalRows - 1,
                Math.Max(firstVisibleRow, CeilingToInt32(offsetRows + viewportRows) - 1)
            );

            var (cacheBefore, cacheAfter) = GetCacheRows(itemsOwner!, viewportRows);

            var firstRow = Math.Max(0, firstVisibleRow - cacheBefore);
            var lastRow  = (int)Math.Min(
                totalRows - 1L,
                (long)lastVisibleRow + cacheAfter
            );

            firstIndex = firstRow * _uniformLayout.Columns;
            lastIndex  = (int)Math.Min(
                _itemCount - 1L,
                ((lastRow + 1L) * _uniformLayout.Columns) - 1L
            );
        }

        if (itemsOwner is not null)
        {
            if (_isVirtualizing)
            {
                CleanupItemsOutsideRange(itemsOwner, firstIndex, lastIndex);
            }

            GenerateAndMeasureItems(
                itemsOwner,
                firstIndex,
                lastIndex,
                new Size(_uniformLayout.ItemWidth, _uniformLayout.ItemHeight)
            );
        }
        else
        {
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(_uniformLayout.ItemWidth, _uniformLayout.ItemHeight));
            }
        }
    }

    private void MeasureNaturalItems(
        ItemsControl? itemsOwner,
        Size          availableSize,
        double        layoutWidth
    )
    {
        if (itemsOwner is not null && _itemCount > 0)
        {
            GenerateAndMeasureItems(
                itemsOwner,
                0,
                _itemCount - 1,
                GetNaturalMeasureConstraint()
            );
        }
        else
        {
            var childConstraint = GetNaturalMeasureConstraint();
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(childConstraint);
            }
        }

        _naturalLayout.Clear();
        _naturalRowWidths.Clear();

        var contentWidth = IsFiniteNonNegative(layoutWidth) ? layoutWidth : double.PositiveInfinity;
        var x            = 0D;
        var y            = 0D;
        var rowHeight    = 0D;
        var widestRow    = 0D;

        foreach (UIElement child in InternalChildren)
        {
            var cellWidth  = GetNaturalItemWidth(child);
            var cellHeight = !double.IsNaN(ItemHeight)
                ? ItemHeight
                : !double.IsNaN(ItemAspectRatio)
                    ? cellWidth / ItemAspectRatio
                    : child.DesiredSize.Height;

            var wraps = x > 0D && !double.IsInfinity(contentWidth) && x + cellWidth > contentWidth;

            if (wraps)
            {
                var completedRowWidth = x - HorizontalSpacing;
                _naturalRowWidths[y] = completedRowWidth;
                widestRow = Math.Max(widestRow, completedRowWidth);
                x = 0D;
                y += rowHeight + VerticalSpacing;
                rowHeight = 0D;
            }

            _naturalLayout[child] = new Rect(x, y, cellWidth, cellHeight);
            x += cellWidth + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, cellHeight);
        }

        if (InternalChildren.Count > 0)
        {
            var completedRowWidth = x - HorizontalSpacing;
            _naturalRowWidths[y] = completedRowWidth;
            widestRow = Math.Max(widestRow, completedRowWidth);
            y += rowHeight;
        }

        var viewportHeight = ResolveNaturalViewportHeight(availableSize.Height, y);
        var viewportWidth  = IsFiniteNonNegative(layoutWidth) ? layoutWidth : widestRow;

        UpdateScrollData(
            widestRow,
            y,
            viewportWidth,
            viewportHeight,
            _scrollData.VerticalOffset
        );
    }

    private void GenerateAndMeasureItems(
        ItemsControl itemsOwner,
        int          firstIndex,
        int          lastIndex,
        Size         constraint
    )
    {
        if (firstIndex > lastIndex || firstIndex < 0)
        {
            return;
        }

        // Panel connects an items host to its generator lazily from InternalChildren. Uniform
        // virtualizing passes normally touch that collection during cleanup first, whereas the
        // natural all-items pass can arrive here directly on its first measure.
        _ = InternalChildren.Count;

        var generator     = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex    = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = generator.GenerateNext(out var newlyRealized) as UIElement;
                if (child is null)
                {
                    break;
                }

                var alreadyAtPosition = childIndex < InternalChildren.Count && InternalChildren[childIndex] == child;
                if (!alreadyAtPosition)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }
                }

                // GenerateNext reports false for an already-realized container and can also return a
                // recycled container. A recycled container is recognizable because it is no longer at
                // the generator position in InternalChildren and must be prepared for its new data item.
                if (newlyRealized || !alreadyAtPosition)
                {
                    generator.PrepareItemContainer(child);
                }

                child.Measure(constraint);
            }
        }
    }

    private void CleanupItemsOutsideRange(
        ItemsControl itemsOwner,
        int          firstIndex,
        int          lastIndex
    )
    {
        var generator = ItemContainerGenerator;
        var recycling =
            GetVirtualizationMode(itemsOwner) == VirtualizationMode.Recycling
            && generator is IRecyclingItemContainerGenerator;

        var cleanupRangeStart = -1;
        var cleanupRangeCount = 0;
        var nextItemIndex     = -1;

        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var child        = InternalChildren[childIndex];
            var itemIndex    = itemsOwner.ItemContainerGenerator.IndexFromContainer(child);
            var outsideRange = itemIndex < firstIndex || itemIndex > lastIndex;

            if (!outsideRange
                || child.IsKeyboardFocusWithin
                || !GetIsContainerVirtualizable(child))
            {
                CleanupRange(generator, recycling, cleanupRangeStart, cleanupRangeCount);
                cleanupRangeStart = -1;
                cleanupRangeCount = 0;
                nextItemIndex = -1;
                continue;
            }

            if (cleanupRangeCount > 0 && itemIndex + 1 != nextItemIndex)
            {
                // A retained focused or explicitly non-virtualizable container can leave a gap in
                // generator indices even when the eligible children happen to be adjacent in the
                // visual collection. Generator range operations require both sequences to remain
                // contiguous, so commit the higher range before beginning another one.
                CleanupRange(generator, recycling, cleanupRangeStart, cleanupRangeCount);
                cleanupRangeCount = 0;
            }

            cleanupRangeStart = childIndex;
            cleanupRangeCount++;
            nextItemIndex = itemIndex;
        }

        CleanupRange(generator, recycling, cleanupRangeStart, cleanupRangeCount);
    }

    /// <summary>
    /// Disconnects one range that is contiguous in both the visual-child and generator sequences.
    /// </summary>
    /// <remarks>
    /// Scrollbar-thumb jumps commonly invalidate every previously visible container. Performing a
    /// single generator operation for that range avoids repeated visual-tree version changes and
    /// generator bookkeeping while retaining the standard and recycling ordering required by WPF.
    /// </remarks>
    private void CleanupRange(
        IItemContainerGenerator generator,
        bool recycling,
        int  childIndex,
        int  count
    )
    {
        if (childIndex < 0 || count <= 0)
        {
            return;
        }

        var position = new GeneratorPosition(childIndex, 0);
        if (recycling)
        {
            ((IRecyclingItemContainerGenerator)generator).Recycle(position, count);
            RemoveInternalChildRange(childIndex, count);
        }
        else
        {
            // Removing one contiguous range is materially cheaper than sending one generator and
            // visual-tree mutation per item during a distant scrollbar-thumb jump. Notify the
            // visual collection first in standard mode, matching WPF's VirtualizingStackPanel
            // ordering; recycling mode must notify the generator first so those containers enter
            // its reuse queue before the next visible range is generated.
            RemoveInternalChildRange(childIndex, count);
            generator.Remove(position, count);
        }
    }

    private void ArrangeUniformItems()
    {
        var verticalTranslation = _usesPixelBasedScrolling
            ? _scrollData.VerticalOffset
            : _scrollData.VerticalOffset * _uniformLayout.RowStride;

        var itemsOwner = ItemsControl.GetItemsOwner(this);
        // Uniform layouts have a stable column lattice. Align that lattice once, rather than
        // aligning each row by its current item count: otherwise a partial final row drifts toward
        // the center or end and no longer lines up with the columns above it. When the collection
        // contains less than one row, its populated columns define the lattice so a genuinely small
        // collection can still be centered or end-aligned as a coherent group.
        var populatedColumnCount = Math.Min(_uniformLayout.Columns, _itemCount);
        var occupiedWidth = GetOccupiedWidth(
            populatedColumnCount,
            _uniformLayout.ItemWidth,
            HorizontalSpacing
        );
        var itemsOrigin = GetItemsAlignmentOffset(_scrollData.ViewportWidth, occupiedWidth);

        foreach (UIElement child in InternalChildren)
        {
            var itemIndex =
                itemsOwner?.ItemContainerGenerator.IndexFromContainer(child)
                ?? InternalChildren.IndexOf(child);

            if (itemIndex < 0)
            {
                continue;
            }

            var row = itemIndex / _uniformLayout.Columns;
            var col = itemIndex % _uniformLayout.Columns;
            var x = itemsOrigin
                    + (col * (_uniformLayout.ItemWidth + HorizontalSpacing))
                    - HorizontalOffset;
            var y   = (row * _uniformLayout.RowStride) - verticalTranslation;

            child.Arrange(
                new Rect(x, y, _uniformLayout.ItemWidth, _uniformLayout.ItemHeight)
            );
        }
    }

    private void ArrangeNaturalItems()
    {
        var verticalTranslation = _scrollData.VerticalOffset;
        foreach (UIElement child in InternalChildren)
        {
            if (_naturalLayout.TryGetValue(child, out var bounds))
            {
                var rowWidth = _naturalRowWidths.TryGetValue(bounds.Y, out var width)
                    ? width
                    : 0D;

                bounds.X += GetItemsAlignmentOffset(_scrollData.ViewportWidth, rowWidth)
                            - HorizontalOffset;
                bounds.Y -= verticalTranslation;
                child.Arrange(bounds);
            }
        }
    }

    /// <summary>
    /// Returns a horizontal layout's leading offset without changing item sizes or spacing.
    /// </summary>
    /// <remarks>
    /// Negative remaining space is clamped to zero so an oversized item retains the same horizontal
    /// scrolling geometry in every alignment mode. Keeping this calculation in arrange means a
    /// runtime alignment change cannot regenerate containers or disturb the virtualized row map.
    /// </remarks>
    private double GetItemsAlignmentOffset(double availableWidth, double occupiedWidth)
    {
        if (!IsFiniteNonNegative(availableWidth) || !IsFiniteNonNegative(occupiedWidth))
        {
            return 0D;
        }

        var remainingWidth = Math.Max(0D, availableWidth - occupiedWidth);
        return ItemsAlignment switch
        {
            VirtualizingWrapPanelItemsAlignment.Center => remainingWidth / 2D,
            VirtualizingWrapPanelItemsAlignment.End    => remainingWidth,
            _                                          => 0D,
        };
    }

    private static double GetOccupiedWidth(
        int    itemCount,
        double itemWidth,
        double horizontalSpacing
    )
    {
        return itemCount <= 0
            ? 0D
            : (itemCount * itemWidth) + (Math.Max(0, itemCount - 1) * horizontalSpacing);
    }

    private Size GetUniformDesiredSize(Size availableSize, double layoutWidth)
    {
        var totalRows   = GetRowCount(_itemCount, _uniformLayout.Columns);
        var pixelHeight = GetUniformPixelExtentHeight(totalRows);

        var desiredHeight =
            ScrollOwner is not null && CanVerticallyScroll && IsFiniteNonNegative(availableSize.Height)
                ? Math.Min(pixelHeight, availableSize.Height)
                : pixelHeight;

        return new Size(layoutWidth, desiredHeight);
    }

    private Size GetNaturalDesiredSize(Size availableSize, double layoutWidth)
    {
        var desiredWidth = IsFiniteNonNegative(availableSize.Width)
            ? layoutWidth
            : _scrollData.ExtentWidth;

        var desiredHeight =
            ScrollOwner is not null && CanVerticallyScroll && IsFiniteNonNegative(availableSize.Height)
                ? Math.Min(_scrollData.ExtentHeight, availableSize.Height)
                : _scrollData.ExtentHeight;

        return new Size(desiredWidth, desiredHeight);
    }

    private Size GetNaturalMeasureConstraint()
    {
        var width = _usesManagedItemWidth
            ? _uniformLayout.ItemWidth
            : double.PositiveInfinity;

        var height = double.IsNaN(ItemHeight) ? double.PositiveInfinity : ItemHeight;
        return new Size(width, height);
    }

    private double GetNaturalItemWidth(UIElement child)
    {
        return _usesManagedItemWidth ? _uniformLayout.ItemWidth : child.DesiredSize.Width;
    }

    private double ResolveLayoutWidth(double availableWidth)
    {
        if (IsFiniteNonNegative(availableWidth))
        {
            return availableWidth;
        }

        if (ScrollOwner is not null && IsFiniteNonNegative(ScrollOwner.ViewportWidth))
        {
            return ScrollOwner.ViewportWidth;
        }

        return IsFiniteNonNegative(ActualWidth) && ActualWidth > 0D
            ? ActualWidth
            : ResolveUnconstrainedLayoutWidth();
    }

    private double ResolveViewportHeight(double availableHeight)
    {
        if (IsFiniteNonNegative(availableHeight))
        {
            return availableHeight;
        }

        if (ScrollOwner is not null && IsFiniteNonNegative(ScrollOwner.ViewportHeight))
        {
            return ScrollOwner.ViewportHeight;
        }

        return double.PositiveInfinity;
    }

    private static double ResolveNaturalViewportHeight(double availableHeight, double extentHeight)
    {
        return IsFiniteNonNegative(availableHeight) ? availableHeight : extentHeight;
    }

    private double GetUniformViewportRows(int totalRows)
    {
        if (totalRows == 0)
        {
            return 0D;
        }

        if (!IsFiniteNonNegative(_pixelViewportHeight) || _uniformLayout.RowStride <= 0D)
        {
            return totalRows;
        }

        // A fractional last row is part of the viewport and is therefore realized. Extent and
        // offset remain row-based, while this ratio preserves an accurate scrollbar thumb size.
        return Math.Min(
            totalRows,
            (_pixelViewportHeight + VerticalSpacing) / _uniformLayout.RowStride
        );
    }

    private double ResolvePixelViewportHeight(double pixelExtentHeight)
    {
        return IsFiniteNonNegative(_pixelViewportHeight)
            ? Math.Min(_pixelViewportHeight, pixelExtentHeight)
            : pixelExtentHeight;
    }

    private double GetUniformPixelExtentHeight(int totalRows)
    {
        return totalRows == 0
            ? 0D
            : (totalRows * _uniformLayout.ItemHeight)
              +
              (Math.Max(0, totalRows - 1) * VerticalSpacing);
    }

    private (int Before, int After) GetCacheRows(
        ItemsControl itemsOwner,
        double       viewportRows
    )
    {
        var cacheLength = GetCacheLength(itemsOwner);
        var unit        = GetCacheLengthUnit(itemsOwner);
        return (
            ConvertCacheLengthToRows(cacheLength.CacheBeforeViewport, unit, viewportRows),
            ConvertCacheLengthToRows(cacheLength.CacheAfterViewport, unit, viewportRows)
        );
    }

    private int ConvertCacheLengthToRows(
        double                        cacheLength,
        VirtualizationCacheLengthUnit unit,
        double                        viewportRows
    )
    {
        if (cacheLength <= 0D)
        {
            return 0;
        }

        var rows = unit switch
        {
            // A row is this panel's logical item in the scrolling direction. Rounding upward also
            // ensures that an item-count cache never realizes only part of a wrap row.
            VirtualizationCacheLengthUnit.Item  => cacheLength / _uniformLayout.Columns,
            VirtualizationCacheLengthUnit.Page  => cacheLength * viewportRows,
            VirtualizationCacheLengthUnit.Pixel => cacheLength / _uniformLayout.RowStride,
            _                                   => 0D,
        };
        return CeilingToInt32(rows);
    }

    private static int GetRowCount(int itemCount, int columns)
    {
        return itemCount == 0 ? 0 : ((itemCount - 1) / columns) + 1;
    }

    private void PreserveVisibleItemAfterColumnChange(
        bool          previouslyUsedUniformLayout,
        bool          previouslyUsedPixelBasedScrolling,
        UniformLayout previousLayout
    )
    {
        if (_itemCount == 0)
        {
            return;
        }

        if (!previouslyUsedUniformLayout)
        {
            // Natural layout offsets are pixels. Preserve the physical position when entering a
            // predictable layout, converting only when the new scroll unit is logical rows.
            if (!_usesPixelBasedScrolling)
            {
                _scrollData = _scrollData with
                {
                    VerticalOffset = _scrollData.VerticalOffset / _uniformLayout.RowStride,
                };
            }

            return;
        }

        if (previousLayout.Columns <= 0)
        {
            return;
        }

        if (previousLayout.Columns == _uniformLayout.Columns
            && previouslyUsedPixelBasedScrolling == _usesPixelBasedScrolling)
        {
            return;
        }

        // Offsets use either logical rows or pixels. Remapping through the leading item preserves
        // the user's visual anchor across a responsive column change or a runtime ScrollUnit change.
        var oldOffsetRows = previouslyUsedPixelBasedScrolling
            ? _scrollData.VerticalOffset / previousLayout.RowStride
            : _scrollData.VerticalOffset;

        var oldFirstRow   = Math.Max(0, (int)Math.Floor(oldOffsetRows));
        var anchorIndex   = (long)oldFirstRow * previousLayout.Columns;
        var boundedAnchor = (int)Math.Min(_itemCount - 1L, anchorIndex);
        var newAnchorRow  = boundedAnchor / _uniformLayout.Columns;

        _scrollData = _scrollData with
        {
            VerticalOffset = _usesPixelBasedScrolling
                ? newAnchorRow * _uniformLayout.RowStride
                : newAnchorRow,
        };
    }

    private void PreserveScrollPositionForNaturalLayout(
        bool          previouslyUsedUniformLayout,
        bool          previouslyUsedPixelBasedScrolling,
        UniformLayout previousLayout
    )
    {
        if (!previouslyUsedUniformLayout || previouslyUsedPixelBasedScrolling)
        {
            return;
        }

        // Natural layout always scrolls in pixels. A logical row offset must therefore be converted
        // before natural scroll data coerces it; otherwise switching a sizing property at runtime
        // would make the viewport jump from (for example) row 20 to pixel 20.
        _scrollData = _scrollData with { VerticalOffset = _scrollData.VerticalOffset * previousLayout.RowStride, };
    }

    private double ResolveUnconstrainedLayoutWidth()
    {
        var fixedItemWidth = ResolveFixedItemWidth(
            ItemWidth,
            MinimumItemWidth,
            MaximumItemWidth,
            ItemAspectRatio,
            ItemHeight
        );

        if (!double.IsNaN(fixedItemWidth))
        {
            return fixedItemWidth;
        }

        if (!double.IsInfinity(MaximumItemWidth))
        {
            return Math.Max(MinimumItemWidth, MaximumItemWidth);
        }

        return MinimumItemWidth > 0D
            ? MinimumItemWidth
            : double.PositiveInfinity;
    }

    private static double ResolveFixedItemWidth(
        double itemWidth,
        double minimumItemWidth,
        double maximumItemWidth,
        double itemAspectRatio,
        double itemHeight
    )
    {
        if (!double.IsNaN(itemWidth))
        {
            return itemWidth;
        }

        if (minimumItemWidth > 0D
            || !double.IsInfinity(maximumItemWidth)
            || double.IsNaN(itemAspectRatio)
            || double.IsNaN(itemHeight))
        {
            return double.NaN;
        }

        // Height and aspect ratio fully determine the missing width. Treat the result as a fixed
        // width rather than a responsive width: changing the viewport may change the column count,
        // but it must not change either explicitly requested height or the resulting aspect ratio.
        // Extremely large, individually valid values can overflow when multiplied; falling back to
        // natural width keeps WPF's measure constraint finite instead of publishing invalid geometry.
        var derivedWidth = itemHeight * itemAspectRatio;
        return derivedWidth > 0D && !double.IsInfinity(derivedWidth)
            ? derivedWidth
            : double.NaN;
    }

    private bool HasResponsiveWidthConstraint()
    {
        return MinimumItemWidth > 0D || !double.IsInfinity(MaximumItemWidth);
    }

    private static int GetColumnCount(double quotient, bool roundUp)
    {
        if (quotient >= int.MaxValue)
        {
            return int.MaxValue;
        }

        var rounded = roundUp ? Math.Ceiling(quotient) : Math.Floor(quotient);
        return Math.Max(1, (int)rounded);
    }

    private static int CeilingToInt32(double value)
    {
        return value >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)Math.Ceiling(value));
    }

    private static bool IsFiniteNonNegative(double value)
    {
        return value >= 0D && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
