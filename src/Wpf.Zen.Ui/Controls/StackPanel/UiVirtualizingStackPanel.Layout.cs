// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Measures complete or virtualized ranges while preserving the visible item anchor.
/// </summary>
public partial class UiVirtualizingStackPanel
{
    // Both tables address the same data indices. Sizes are physical DIPs; counts assign
    // one logical unit to each non-collapsed slot, including slots not yet measured.
    private StackItemMetrics _itemSizes  = new(0);
    private StackItemMetrics _itemCounts = new(0, 1);

    private Size        _lastChildConstraint;
    private Orientation _lastOrientation;
    private double      _lastSpacing;

    // The last visible container survives ordinary edits. Pending anchors carry its updated
    // data index into the next measure, before that pass clears the measured-container map.
    private UIElement?    _anchorContainer;
    private double        _anchorOffsetWithinItem;
    private LayoutAnchor? _pendingLayoutAnchor;

    /// <summary>
    /// Keeps a visible item at the same physical position while unknown sizes are re-estimated.
    /// </summary>
    /// <param name="Index">The data index used as the origin of a measure pass.</param>
    /// <param name="OffsetWithinItem">The viewport's pixel offset inside the item's slot.</param>
    private readonly record struct LayoutAnchor(int Index, double OffsetWithinItem);

    /// <summary>
    /// Describes a realization range relative to an anchor whose physical position can change.
    /// Item-based trailing cache is measured by count after reaching the viewport's physical end.
    /// </summary>
    /// <param name="FirstIndex">The first data index to realize.</param>
    /// <param name="EndFromAnchor">The physical end relative to the anchor's slot start.</param>
    /// <param name="TrailingItems">Additional items to realize after reaching that end.</param>
    private readonly record struct CacheRange(int FirstIndex, double EndFromAnchor, int TrailingItems);

    /// <summary>
    /// Leaves the stacking direction unconstrained and permits cross-axis overflow when scrollable.
    /// </summary>
    private Size GetChildMeasureConstraint(Size availableSize)
    {
        var crossConstraint = CanScrollAcross
            ? double.PositiveInfinity
            : StackSpacing.Across(availableSize, Orientation);

        return StackSpacing.Size(double.PositiveInfinity, crossConstraint, Orientation);
    }

    /// <summary>
    /// Preserves the visible anchor, invalidates stale size estimates, and starts a fresh pass.
    /// Spacing changes keep measured lengths; orientation or child constraints require new lengths.
    /// </summary>
    private void PrepareMeasure(ItemsControl? owner, int itemCount, Size constraint, Size availableSize)
    {
        var orientationChanged = _lastOrientation != Orientation;
        var constraintChanged  = _lastChildConstraint != constraint;
        var spacingChanged     = _lastSpacing != Spacing;

        if (!orientationChanged && (constraintChanged || spacingChanged)
            && !_pendingLayoutAnchor.HasValue && _anchorContainer is not null
            && _measuredIndices.TryGetValue(_anchorContainer, out var anchorIndex))
        {
            _pendingLayoutAnchor = new LayoutAnchor(anchorIndex, _anchorOffsetWithinItem);
        }

        if (_itemSizes.Count != itemCount || constraintChanged || orientationChanged)
        {
            ResetItemMetrics(itemCount);
        }

        if (orientationChanged)
        {
            _stackOffset         = 0;
            _crossOffset         = 0;
            _pendingLayoutAnchor = null;
            _anchorContainer     = null;
        }

        _lastChildConstraint = constraint;
        _lastOrientation     = Orientation;
        _lastSpacing         = Spacing;
        _usesPixelScrolling  = owner is null || GetScrollUnit(owner) == ScrollUnit.Pixel;

        var availableLength = StackSpacing.Along(availableSize, Orientation);

        var virtualizationActive = CanVirtualize(owner, availableLength);
        SetValue(IsVirtualizationActivePropertyKey, virtualizationActive);

        _stackViewport = double.IsInfinity(availableLength) ? 0 : availableLength;

        _measuredIndices.Clear();

        // Recompute breadth so shrinking content can remove obsolete cross-axis scroll space.
        _crossExtent = 0;
    }

    /// <summary>
    /// Limits virtualization to flat item hosts with their own finite scrolling viewport.
    /// Grouped views and tree hosts always take the complete-layout path.
    /// </summary>
    private bool CanVirtualize(ItemsControl? owner, double availableLength)
    {
        return owner is not null && owner.Items.Groups is null
            && owner is not System.Windows.Controls.TreeView
            && owner is not System.Windows.Controls.TreeViewItem
            && GetIsVirtualizing(owner) && ScrollOwner is not null
            && CanScrollAlong && !double.IsInfinity(availableLength);
    }

    /// <summary>
    /// Replaces both size tables together so physical and logical slots share one data sequence.
    /// </summary>
    private void ResetItemMetrics(int count)
    {
        _itemSizes  = new StackItemMetrics(count);
        _itemCounts = new StackItemMetrics(count, 1);
    }

    /// <summary>
    /// Finalizes scroll geometry and consumes requests only after all selected items are measured.
    /// A scrolling host reports its viewport size; other hosts report their full desired size.
    /// </summary>
    private Size CompleteMeasure(Size availableSize)
    {
        var extent           = _itemSizes.Extent(Spacing);
        var availableLength  = StackSpacing.Along(availableSize, Orientation);
        var availableBreadth = StackSpacing.Across(availableSize, Orientation);

        _stackViewport       = double.IsInfinity(availableLength) ? extent : availableLength;
        _stackOffset         = CanScrollAlong ? ClampOffset(_stackOffset, extent, _stackViewport) : 0;
        _crossViewport       = double.IsInfinity(availableBreadth) ? _crossExtent : availableBreadth;
        _crossOffset         = CanScrollAcross ? ClampOffset(_crossOffset, _crossExtent, _crossViewport) : 0;
        _requestedItemIndex  = null;
        _pendingLayoutAnchor = null;
        _scrollToEnd         = false;

        PublishScrollInfo();

        if (ScrollOwner is null)
        {
            return StackSpacing.Size(extent, _crossExtent, Orientation);
        }

        var desiredLength  = Math.Min(extent, _stackViewport);
        var desiredBreadth = Math.Min(_crossExtent, _crossViewport);
        return StackSpacing.Size(desiredLength, desiredBreadth, Orientation);
    }

    /// <summary>
    /// Measures directly added children, which have no item generator or virtualization lifecycle.
    /// </summary>
    private void MeasureDirectChildren(UIElementCollection children, Size constraint)
    {
        for (var index = 0; index < children.Count; index++)
        {
            MeasureContainer(children[index], index, constraint);
        }
    }

    /// <summary>
    /// Measures the complete generator sequence, including groups in the nonvirtualized fallback.
    /// </summary>
    private void MeasureAllContainers(Size constraint)
    {
        var containers = GenerateAllContainers();
        if (_itemSizes.Count != containers.Count)
        {
            ResetItemMetrics(containers.Count);
        }

        for (var index = 0; index < containers.Count; index++)
        {
            MeasureContainer(containers[index], index, constraint);
        }
    }

    /// <summary>
    /// Measures the viewport and cache around a stable item anchor. Repeat only when revised
    /// estimates clamp the offset and expose a different range, most commonly at the list end.
    /// </summary>
    private void MeasureVisibleContainers(ItemsControl owner, Size constraint)
    {
        var anchor = SelectMeasureAnchor();
        while (true)
        {
            _stackOffset = _itemSizes.Start(anchor.Index, Spacing) + anchor.OffsetWithinItem;

            var cache     = GetCacheRange(owner, anchor);
            var lastIndex = MeasureCacheRange(anchor, cache, constraint);

            // Measuring changes the average for unknown items, including those before the anchor.
            var measuredOffset = _itemSizes.Start(anchor.Index, Spacing) + anchor.OffsetWithinItem;
            var extent         = _itemSizes.Extent(Spacing);

            var clampedOffset = _scrollToEnd
                ? Math.Max(0, extent - _stackViewport)
                : ClampOffset(measuredOffset, extent, _stackViewport);

            var nextIndex = _itemSizes.Find(clampedOffset, Spacing);

            var offsetCorrection = Math.Abs(clampedOffset - measuredOffset);
            if (nextIndex < cache.FirstIndex || offsetCorrection > 0.01)
            {
                var nextStart        = _itemSizes.Start(nextIndex, Spacing);
                var offsetWithinItem = clampedOffset - nextStart;
                anchor = new LayoutAnchor(nextIndex, offsetWithinItem);

                continue;
            }

            _stackOffset = clampedOffset;
            RemoveContainersOutsideRange(owner, cache.FirstIndex, lastIndex);
            break;
        }

        MeasureRetainedContainers(constraint);
        CaptureVisibleAnchor();
    }

    /// <summary>
    /// Gives explicit item and end requests priority over an edit/resize anchor or current offset.
    /// Called only for a nonempty virtualized sequence.
    /// </summary>
    private LayoutAnchor SelectMeasureAnchor()
    {
        var lastIndex = _itemSizes.Count - 1;

        if (_requestedItemIndex is { } requestedIndex)
        {
            var requestedAnchorIndex = Math.Min(requestedIndex, lastIndex);
            return new LayoutAnchor(requestedAnchorIndex, 0);
        }

        if (_scrollToEnd)
        {
            return new LayoutAnchor(lastIndex, 0);
        }

        if (_pendingLayoutAnchor is { } pending)
        {
            var pendingAnchorIndex = Math.Min(pending.Index, lastIndex);
            return new LayoutAnchor(pendingAnchorIndex, pending.OffsetWithinItem);
        }

        var index = _itemSizes.Find(_stackOffset, Spacing);

        var itemStart   = _itemSizes.Start(index, Spacing);
        var localOffset = Math.Max(0, _stackOffset - itemStart);

        return new LayoutAnchor(index, localOffset);
    }

    /// <summary>
    /// Realizes through the physical viewport/cache end, then adds any item-count trailing cache.
    /// Distances use the current anchor position because each measured item can change estimates.
    /// </summary>
    private int MeasureCacheRange(LayoutAnchor anchor, CacheRange cache, Size constraint)
    {
        var lastIndex = cache.FirstIndex;

        for (var index = cache.FirstIndex; index < _itemSizes.Count; index++)
        {
            var child = GetOrCreateContainer(index);
            MeasureContainer(child, index, constraint);

            lastIndex = index;

            var itemStart   = _itemSizes.Start(index, Spacing);
            var itemLength  = _itemSizes.Length(index);
            var anchorStart = _itemSizes.Start(anchor.Index, Spacing);
            var relativeEnd = itemStart + itemLength - anchorStart;

            if (index < anchor.Index || relativeEnd < cache.EndFromAnchor)
            {
                continue;
            }

            for (var extra = 0; extra < cache.TrailingItems && lastIndex + 1 < _itemSizes.Count; extra++)
            {
                lastIndex++;
                var trailingChild = GetOrCreateContainer(lastIndex);
                MeasureContainer(trailingChild, lastIndex, constraint);
            }

            break;
        }

        return lastIndex;
    }

    /// <summary>
    /// Converts cache policy to a start index, a physical end and a trailing item count.
    /// Page units use the viewport length; item units preserve exact direct-container counts.
    /// </summary>
    private CacheRange GetCacheRange(ItemsControl owner, LayoutAnchor anchor)
    {
        var cache = GetCacheLength(owner);
        var unit  = GetCacheLengthUnit(owner);

        if (unit == VirtualizationCacheLengthUnit.Item)
        {
            var requestedBefore       = Math.Ceiling(cache.CacheBeforeViewport);
            var requestedAfter        = Math.Ceiling(cache.CacheAfterViewport);
            var before                = (int)Math.Min(int.MaxValue, requestedBefore);
            var after                 = (int)Math.Min(int.MaxValue, requestedAfter);
            var firstCachedIndex      = Math.Max(0, anchor.Index - before);
            var viewportEndFromAnchor = anchor.OffsetWithinItem + _stackViewport;
            return new CacheRange(firstCachedIndex, viewportEndFromAnchor, after);
        }

        var scale = unit == VirtualizationCacheLengthUnit.Page ? _stackViewport : 1;
        var start = _itemSizes.Start(anchor.Index, Spacing)
            + anchor.OffsetWithinItem
            - (cache.CacheBeforeViewport * scale);

        var cacheStart = Math.Max(0, start);
        var firstIndex = _itemSizes.Find(cacheStart, Spacing);

        var end = anchor.OffsetWithinItem + _stackViewport + (cache.CacheAfterViewport * scale);

        return new CacheRange(firstIndex, end, 0);
    }

    /// <summary>
    /// Updates containers kept outside the cache by focus, cancellation or virtualization settings.
    /// The generator still owns these visuals, so they must receive current indices and sizes.
    /// </summary>
    private void MeasureRetainedContainers(Size constraint)
    {
        for (var position = 0; position < InternalChildren.Count; position++)
        {
            var child             = InternalChildren[position];
            var generatorPosition = new GeneratorPosition(position, 0);
            var index             = ItemContainerGenerator.IndexFromGeneratorPosition(generatorPosition);

            if (!_measuredIndices.ContainsKey(child) && index >= 0 && index < _itemSizes.Count)
            {
                MeasureContainer(child, index, constraint);
            }
        }
    }

    /// <summary>
    /// Saves the visible container identity and local pixel offset for later resizing or edits.
    /// </summary>
    private void CaptureVisibleAnchor()
    {
        var index = _itemSizes.Find(_stackOffset, Spacing);

        var anchorEntry = _measuredIndices.FirstOrDefault(pair => pair.Value == index);
        _anchorContainer        = anchorEntry.Key;
        _anchorOffsetWithinItem = _stackOffset - _itemSizes.Start(index, Spacing);
    }

    /// <summary>
    /// Measures a container without changing its properties, then records its size and visibility.
    /// </summary>
    private void MeasureContainer(UIElement child, int index, Size constraint)
    {
        child.Measure(constraint);

        var length    = StackSpacing.Along(child.DesiredSize, Orientation);
        var breadth   = StackSpacing.Across(child.DesiredSize, Orientation);
        var collapsed = child.Visibility == Visibility.Collapsed;

        _itemSizes.Set(index, length, collapsed);
        _itemCounts.Set(index, 1, collapsed);

        _crossExtent = Math.Max(_crossExtent, breadth);

        _measuredIndices[child] = index;
    }
}