// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnCollection = System.Windows.Controls.GridViewColumnCollection;

namespace Wpf.Ui.Controls;

internal static class GridViewColumnLayout
{
    // Both native GridView presenters append this fixed padding header after all data columns.
    private const double NativePaddingHeaderWidth = 2.0;

    internal static bool HasFiniteWidth(double width)
    {
        return !double.IsNaN(width) && !double.IsInfinity(width) && width >= 0.0;
    }

    internal static void PrepareForMeasure(NativeGridViewColumnCollection? columns, double availableWidth)
    {
        if (columns is null)
        {
            return;
        }

        bool hasFiniteWidth = HasFiniteWidth(availableWidth);
        foreach (GridViewColumn column in columns.OfType<GridViewColumn>())
        {
            column.PrepareForMeasure(hasFiniteWidth);
            column.CoerceAutoDesiredWidth();
        }
    }

    internal static void CoerceAutoColumns(NativeGridViewColumnCollection? columns)
    {
        if (columns is null)
        {
            return;
        }

        foreach (GridViewColumn column in columns.OfType<GridViewColumn>())
        {
            column.CoerceAutoDesiredWidth();
        }
    }

    internal static bool ResolveStarColumns(NativeGridViewColumnCollection? columns, double availableWidth)
    {
        if (columns is null || !HasFiniteWidth(availableWidth))
        {
            return false;
        }

        var starColumns = new List<StarColumn>();
        double occupiedWidth = 0.0;

        foreach (NativeGridViewColumn nativeColumn in columns)
        {
            if (nativeColumn is GridViewColumn column && column.Width.IsStar)
            {
                starColumns.Add(new StarColumn(column, column.Width.Value));
            }
            else
            {
                occupiedWidth += nativeColumn.ActualWidth;
            }
        }

        if (starColumns.Count == 0)
        {
            return false;
        }

        double remainingSpace = Math.Max(0.0, availableWidth - NativePaddingHeaderWidth - occupiedWidth);
        double remainingWeight = starColumns.Sum(static column => column.Weight);
        var unresolvedColumns = new List<StarColumn>(starColumns);

        bool foundConstraint = true;
        while (foundConstraint && unresolvedColumns.Count > 0)
        {
            foundConstraint = false;

            for (int index = unresolvedColumns.Count - 1; index >= 0; index--)
            {
                StarColumn starColumn = unresolvedColumns[index];
                double proposedWidth =
                    remainingWeight > 0.0 ? remainingSpace * starColumn.Weight / remainingWeight : 0.0;
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
            double proposedWidth =
                remainingWeight > 0.0 ? remainingSpace * starColumn.Weight / remainingWeight : 0.0;
            starColumn.ResolvedWidth = starColumn.Column.CoerceWidth(proposedWidth);
        }

        bool widthChanged = false;
        foreach (StarColumn starColumn in starColumns)
        {
            widthChanged |= starColumn.Column.SetStarActualWidth(starColumn.ResolvedWidth);
        }

        return widthChanged;
    }

    private static bool AreClose(double first, double second)
    {
        double delta = Math.Abs(first - second);
        return delta < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1.0);
    }

    private sealed class StarColumn
    {
        public StarColumn(GridViewColumn column, double weight)
        {
            Column = column;
            Weight = weight;
        }

        public GridViewColumn Column { get; }

        public double Weight { get; }

        public double ResolvedWidth { get; set; }
    }
}
