// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Caches measured item lengths in a Fenwick tree. Unknown items use the current measured average;
/// prefix lookup and individual updates take logarithmic time without realizing offscreen content.
/// </summary>
internal sealed class StackItemMetrics
{
    private readonly double[] _lengths;
    private readonly double[] _sums;
    private readonly int[]    _known;
    private readonly int[]    _collapsed;

    private double _totalLength;
    private int    _measuredCount;
    private int    _collapsedCount;

    /// <summary>
    /// Creates empty prefix tables with an initial length for each unmeasured item.
    /// </summary>
    internal StackItemMetrics(int count, double initialEstimate = 24)
    {
        Count    = count;
        Estimate = initialEstimate;
        _lengths = [.. Enumerable.Repeat(double.NaN, count)];
        _sums      = new double[count + 1];
        _known     = new int[count + 1];
        _collapsed = new int[count + 1];
    }

    /// <summary>
    /// Gets the number of data slots, including collapsed and unmeasured items.
    /// </summary>
    internal int Count { get; }

    /// <summary>
    /// Gets the average visible length, or the initial estimate before measuring a visible item.
    /// </summary>
    internal double Estimate
    {
        get
        {
            var visibleCount = _measuredCount - _collapsedCount;
            if (visibleCount == 0)
            {
                return field;
            }

            var averageLength = _totalLength / visibleCount;
            return Math.Max(1, averageLength);
        }
    }

    /// <summary>
    /// Records a measured slot and updates the prefix tables and average length.
    /// </summary>
    internal void Set(int index, double length, bool collapsed)
    {
        var previous = _lengths[index];
        var value    = collapsed ? -1 : length;

        if (previous == value)
        {
            return;
        }

        var wasUnknown        = double.IsNaN(previous);
        var previousLength    = wasUnknown ? 0 : Math.Max(0, previous);
        var currentLength     = Math.Max(0, value);
        var previousCollapsed = previous == -1 ? 1 : 0;
        var currentCollapsed  = collapsed ? 1 : 0;
        var delta             = currentLength - previousLength;
        var knownDelta        = wasUnknown ? 1 : 0;
        var collapsedDelta    = currentCollapsed - previousCollapsed;

        _lengths[index] =  value;
        _totalLength    += delta;
        _measuredCount  += knownDelta;
        _collapsedCount += collapsedDelta;

        for (var node = index + 1; node <= Count; node += node & -node)
        {
            _sums[node]      += delta;
            _known[node]     += knownDelta;
            _collapsed[node] += collapsedDelta;
        }
    }

    /// <summary>
    /// Reports whether a measured slot contributes neither length nor spacing.
    /// </summary>
    internal bool IsCollapsed(int index)
    {
        return _lengths[index] == -1;
    }

    /// <summary>
    /// Returns the measured length, zero for collapsed slots, or the current estimate.
    /// </summary>
    internal double Length(int index)
    {
        var length = _lengths[index];
        if (double.IsNaN(length))
        {
            return Estimate;
        }

        return Math.Max(0, length);
    }

    /// <summary>
    /// Returns the item start including one gap for each preceding non-collapsed item. The extent
    /// removes the final gap, so no terminal whitespace is created by the estimate.
    /// </summary>
    internal double Start(int index, double spacing)
    {
        var sum       = 0D;
        var known     = 0;
        var collapsed = 0;

        for (var node = index; node > 0; node -= node & -node)
        {
            sum       += _sums[node];
            known     += _known[node];
            collapsed += _collapsed[node];
        }

        var estimatedLength = (index - known) * Estimate;
        var gapLength       = (index - collapsed) * spacing;
        return sum + estimatedLength + gapLength;
    }

    /// <summary>
    /// Returns the total estimated content length without a trailing gap.
    /// </summary>
    internal double Extent(double spacing)
    {
        var totalLength     = Start(Count, spacing);
        var trailingSpacing = Count > _collapsedCount ? spacing : 0;
        return Math.Max(0, totalLength - trailingSpacing);
    }

    /// <summary>
    /// Finds the slot at a physical offset, clamping to the available index range.
    /// </summary>
    internal int Find(double position, double spacing)
    {
        var index    = 0;
        var prefix   = 0D;
        var estimate = Estimate;
        var bit      = 1;

        while (bit <= Count / 2)
        {
            bit <<= 1;
        }

        // Binary lifting searches the weighted tree directly, including estimated unknown lengths.
        // This avoids a second logarithmic factor from repeatedly querying prefix sums.
        for (; bit > 0; bit >>= 1)
        {
            var next = index + bit;
            if (next > Count)
            {
                continue;
            }

            var covered         = next & -next;
            var estimatedLength = (covered - _known[next]) * estimate;
            var gapLength       = (covered - _collapsed[next]) * spacing;
            var length          = _sums[next] + estimatedLength + gapLength;

            if (prefix + length <= position)
            {
                index  =  next;
                prefix += length;
            }
        }

        var lastIndex = Math.Max(0, Count - 1);
        return Math.Min(index, lastIndex);
    }
}