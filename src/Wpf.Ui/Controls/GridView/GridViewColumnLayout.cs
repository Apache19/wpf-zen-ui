// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnCollection = System.Windows.Controls.GridViewColumnCollection;

namespace Wpf.Ui.Controls;

/// <summary>
/// Contains the deterministic, visual-tree-independent part of GridView column allocation.
/// </summary>
internal static class GridViewColumnLayout
{
    // Both native WPF presenters reserve this amount for the padding header appended after the last
    // data column. Excluding it from star allocation keeps header and row desired widths equal to
    // the ScrollViewer viewport and avoids a permanent two-pixel horizontal extent.
    private const double NativePaddingHeaderWidth = 2.0;

    /// <summary>
    /// Applies declared bounds after WPF has measured Auto headers and cells.
    /// </summary>
    internal static void CoerceAutoColumns(NativeGridViewColumnCollection columns)
    {
        foreach (GridViewColumn column in columns.OfType<GridViewColumn>())
        {
            column.CoerceAutoDesiredWidth();
        }
    }

    /// <summary>
    /// Applies an interactive resize request to a star column while retaining star sizing.
    /// </summary>
    /// <remarks>
    /// WPF's GridView header writes a finite value to the native double Width property for every
    /// drag. Treating that write as a new pixel definition permanently removes one star column on
    /// each resize. Instead, the boundary's star columns to the right absorb the delta, which is
    /// the same directional contract used by WPF DataGrid and GridSplitter. Adjustable Auto and
    /// fixed columns to the right participate after the available star capacity is exhausted. As
    /// with native GridView resizing, an Auto column changed by that interaction becomes fixed.
    /// </remarks>
    internal static bool TryResizeStarColumn(
        NativeGridViewColumnCollection columns,
        GridViewColumn resizingColumn,
        double previousWidth,
        double requestedWidth,
        double availableWidth,
        double leadingContentWidth = 0.0
    )
    {
        if (
            !resizingColumn.Width.IsStar
            || !IsFiniteNonNegative(previousWidth)
            || !IsFiniteNonNegative(requestedWidth)
            || !IsFiniteNonNegative(availableWidth)
            || !IsFiniteNonNegative(leadingContentWidth)
        )
        {
            return false;
        }

        int resizingIndex = columns.IndexOf(resizingColumn);
        if (resizingIndex < 0)
        {
            return false;
        }

        var starColumns = new List<GridViewColumn>();
        var rightStarColumns = new List<GridViewColumn>();
        var rightNonStarColumns = new List<GridViewColumn>();
        var desiredWidths = new Dictionary<GridViewColumn, double>();
        double nonStarSpace = 0.0;

        for (int index = 0; index < columns.Count; index++)
        {
            NativeGridViewColumn nativeColumn = columns[index];
            if (nativeColumn is GridViewColumn column)
            {
                double actualWidth = ReferenceEquals(column, resizingColumn)
                    ? previousWidth
                    : GetFiniteWidth(column);
                desiredWidths.Add(column, column.CoerceWidth(actualWidth));

                if (column.Width.IsStar)
                {
                    starColumns.Add(column);
                    if (index > resizingIndex)
                    {
                        rightStarColumns.Add(column);
                    }
                }
                else
                {
                    nonStarSpace += column.ActualWidth;
                    if (index > resizingIndex)
                    {
                        rightNonStarColumns.Add(column);
                    }
                }
            }
            else if (IsFiniteNonNegative(nativeColumn.ActualWidth))
            {
                nonStarSpace += nativeColumn.ActualWidth;
            }
        }

        // A trailing star boundary has no definition to its right that can absorb the change.
        // Marking the request handled restores the current allocation instead of silently turning
        // the star into a fixed pixel column.
        if (rightStarColumns.Count == 0 && rightNonStarColumns.Count == 0)
        {
            _ = ResolveStarColumns(columns, availableWidth, leadingContentWidth);
            return true;
        }

        double currentWidth = desiredWidths[resizingColumn];
        double targetWidth = resizingColumn.CoerceWidth(requestedWidth);
        double availableStarSpace = Math.Max(
            0.0,
            availableWidth - NativePaddingHeaderWidth - leadingContentWidth - nonStarSpace
        );
        double currentStarSpace = starColumns.Sum(column => desiredWidths[column]);

        if (targetWidth > currentWidth)
        {
            double requestedGrowth = targetWidth - currentWidth;
            double unusedSpace = Math.Max(0.0, availableStarSpace - currentStarSpace);
            double growthFromUnusedSpace = Math.Min(requestedGrowth, unusedSpace);
            double remainingGrowth = requestedGrowth - growthFromUnusedSpace;
            double growthFromRightStars = RedistributeWidth(
                rightStarColumns,
                desiredWidths,
                remainingGrowth,
                shrink: true
            );
            double growthFromRightNonStars = RedistributeWidth(
                rightNonStarColumns,
                desiredWidths,
                remainingGrowth - growthFromRightStars,
                shrink: true,
                useDefinitionWeights: false
            );
            desiredWidths[resizingColumn] =
                currentWidth
                + growthFromUnusedSpace
                + growthFromRightStars
                + growthFromRightNonStars;
        }
        else if (targetWidth < currentWidth)
        {
            double requestedShrink = currentWidth - targetWidth;
            double shrinkAbsorbedByRightStars = RedistributeWidth(
                rightStarColumns,
                desiredWidths,
                requestedShrink,
                shrink: false
            );
            double shrinkAbsorbedByRightNonStars = RedistributeWidth(
                rightNonStarColumns,
                desiredWidths,
                requestedShrink - shrinkAbsorbedByRightStars,
                shrink: false,
                useDefinitionWeights: false
            );
            desiredWidths[resizingColumn] =
                currentWidth - shrinkAbsorbedByRightStars - shrinkAbsorbedByRightNonStars;
        }

        // WPF GridView has no separate desired/display widths for non-star columns. Once an Auto or
        // fixed column absorbs a manual boundary change, publish the resulting pixel definition;
        // this is the same observable result as dragging that column's own native gripper.
        foreach (GridViewColumn column in rightNonStarColumns)
        {
            if (!AreClose(column.ActualWidth, desiredWidths[column]))
            {
                column.SetInteractiveWidth(desiredWidths[column]);
            }
        }

        // Star values are ratios, so normalize the new pixel proportions back to the original
        // aggregate weight. This keeps XAML-scale weights readable while preserving the exact
        // boundary selected by the user. Constraint-bound columns are resolved by the normal pass.
        double originalWeight = starColumns.Sum(static column => column.Width.Value);
        double desiredTotal = starColumns.Sum(column => desiredWidths[column]);
        double weightScale = originalWeight > 0.0 && desiredTotal > 0.0
            ? originalWeight / desiredTotal
            : 1.0;

        foreach (GridViewColumn starColumn in starColumns)
        {
            starColumn.SetStarWeight(desiredWidths[starColumn] * weightScale);
        }

        _ = ResolveStarColumns(columns, availableWidth, leadingContentWidth);
        return true;
    }

    /// <summary>
    /// Resolves all star columns against a finite viewport while honoring minimum and maximum widths.
    /// </summary>
    /// <remarks>
    /// Auto and fixed columns consume their measured or declared widths; only star columns absorb
    /// unused viewport space. This deliberately follows GridLength semantics: stretching Auto would
    /// make content sizing indistinguishable from star sizing. Consequently, the data columns fill
    /// the viewport whenever at least one star column exists and its MinWidth/MaxWidth constraints
    /// permit it. Unsatisfiable minimums may overflow, while maximums may leave unused padding.
    /// </remarks>
    internal static bool ResolveStarColumns(
        NativeGridViewColumnCollection? columns,
        double availableWidth,
        double leadingContentWidth = 0.0
    )
    {
        if (
            columns is null
            || !IsFiniteNonNegative(availableWidth)
            || !IsFiniteNonNegative(leadingContentWidth)
        )
        {
            return false;
        }

        var starColumns = new List<StarColumn>();
        double occupiedWidth = 0.0;

        foreach (NativeGridViewColumn nativeColumn in columns)
        {
            if (nativeColumn is GridViewColumn { Width.IsStar: true } column)
            {
                starColumns.Add(new StarColumn(column, column.Width.Value));
                continue;
            }

            if (IsFiniteNonNegative(nativeColumn.ActualWidth))
            {
                occupiedWidth += nativeColumn.ActualWidth;
            }
        }

        if (starColumns.Count == 0)
        {
            return false;
        }

        double remainingSpace = Math.Max(
            0.0,
            availableWidth - NativePaddingHeaderWidth - leadingContentWidth - occupiedWidth
        );
        double remainingWeight = starColumns.Sum(static column => column.Weight);
        var unresolvedColumns = new List<StarColumn>(starColumns);

        // This is the same iterative constraint resolution used conceptually by Grid: columns that
        // hit MinWidth or MaxWidth leave the proportional pool, then the remaining space is divided
        // again among unconstrained stars. A single proportional pass produces incorrect totals
        // whenever one star is clamped.
        bool foundConstraint = true;
        while (foundConstraint && unresolvedColumns.Count > 0)
        {
            foundConstraint = false;
            for (int index = unresolvedColumns.Count - 1; index >= 0; index--)
            {
                StarColumn starColumn = unresolvedColumns[index];
                double proposedWidth = remainingWeight > 0.0
                    ? remainingSpace * starColumn.Weight / remainingWeight
                    : 0.0;
                double constrainedWidth = starColumn.Column.CoerceWidth(proposedWidth);

                if (starColumn.Weight > 0.0 && AreClose(proposedWidth, constrainedWidth))
                {
                    continue;
                }

                starColumn.ResolvedWidth = constrainedWidth;
                remainingSpace = Math.Max(0.0, remainingSpace - constrainedWidth);
                remainingWeight = Math.Max(0.0, remainingWeight - starColumn.Weight);
                unresolvedColumns.RemoveAt(index);
                foundConstraint = true;
            }
        }

        foreach (StarColumn starColumn in unresolvedColumns)
        {
            double proposedWidth = remainingWeight > 0.0
                ? remainingSpace * starColumn.Weight / remainingWeight
                : 0.0;
            starColumn.ResolvedWidth = starColumn.Column.CoerceWidth(proposedWidth);
        }

        bool widthChanged = false;
        foreach (StarColumn starColumn in starColumns)
        {
            widthChanged |= starColumn.Column.SetStarActualWidth(starColumn.ResolvedWidth);
        }

        return widthChanged;
    }

    private static bool IsFiniteNonNegative(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;
    }

    private static double GetFiniteWidth(GridViewColumn column)
    {
        if (IsFiniteNonNegative(column.NativeWidth))
        {
            return column.NativeWidth;
        }

        return IsFiniteNonNegative(column.ActualWidth) ? column.ActualWidth : column.MinWidth;
    }

    /// <summary>
    /// Distributes a boundary delta proportionally, removing columns from the pool as they reach a
    /// declared minimum or maximum. The return value is the amount the right side could absorb.
    /// </summary>
    private static double RedistributeWidth(
        IReadOnlyCollection<GridViewColumn> columns,
        IDictionary<GridViewColumn, double> widths,
        double requestedChange,
        bool shrink,
        bool useDefinitionWeights = true
    )
    {
        if (requestedChange <= 0.0)
        {
            return 0.0;
        }

        double remainingChange = requestedChange;
        var unresolvedColumns = new List<GridViewColumn>(columns);

        while (remainingChange > 0.000001 && unresolvedColumns.Count > 0)
        {
            double totalWeight = useDefinitionWeights
                ? unresolvedColumns.Sum(static column => column.Width.Value)
                : unresolvedColumns.Count;
            bool useEqualWeights = totalWeight <= 0.0;
            if (useEqualWeights)
            {
                totalWeight = unresolvedColumns.Count;
            }

            bool foundConstraint = false;
            for (int index = unresolvedColumns.Count - 1; index >= 0; index--)
            {
                GridViewColumn column = unresolvedColumns[index];
                double weight = useEqualWeights || !useDefinitionWeights ? 1.0 : column.Width.Value;
                double proposedChange = remainingChange * weight / totalWeight;
                double capacity = shrink
                    ? Math.Max(0.0, widths[column] - column.MinWidth)
                    : Math.Max(0.0, column.MaxWidth - widths[column]);

                if (capacity > proposedChange && !AreClose(capacity, proposedChange))
                {
                    continue;
                }

                ApplyChange(column, widths, capacity, shrink);
                remainingChange = Math.Max(0.0, remainingChange - capacity);
                unresolvedColumns.RemoveAt(index);
                foundConstraint = true;
            }

            if (foundConstraint)
            {
                continue;
            }

            double changeForPass = remainingChange;
            foreach (GridViewColumn column in unresolvedColumns)
            {
                double weight = useEqualWeights || !useDefinitionWeights ? 1.0 : column.Width.Value;
                ApplyChange(column, widths, changeForPass * weight / totalWeight, shrink);
            }

            remainingChange = 0.0;
        }

        return requestedChange - remainingChange;
    }

    private static void ApplyChange(
        GridViewColumn column,
        IDictionary<GridViewColumn, double> widths,
        double change,
        bool shrink
    )
    {
        widths[column] = shrink ? widths[column] - change : widths[column] + change;
    }

    private static bool AreClose(double first, double second)
    {
        double delta = Math.Abs(first - second);
        return delta < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1.0);
    }

    private sealed class StarColumn
    {
        internal StarColumn(GridViewColumn column, double weight)
        {
            Column = column;
            Weight = weight;
        }

        internal GridViewColumn Column { get; }

        internal double Weight { get; }

        internal double ResolvedWidth { get; set; }
    }
}
