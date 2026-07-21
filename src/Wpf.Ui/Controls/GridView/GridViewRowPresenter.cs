// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Presents a GridView row and coordinates Auto, pixel, and star-sized columns.
/// </summary>
public class GridViewRowPresenter : System.Windows.Controls.GridViewRowPresenter
{
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        _ = GridViewColumnLayout.ResolveStarColumns(Columns, arrangeSize.Width);

        return base.ArrangeOverride(arrangeSize);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        GridViewColumnLayout.PrepareForMeasure(Columns, constraint.Width);
        Size desiredSize = base.MeasureOverride(constraint);
        GridViewColumnLayout.CoerceAutoColumns(Columns);

        if (GridViewColumnLayout.ResolveStarColumns(Columns, constraint.Width))
        {
            desiredSize = base.MeasureOverride(constraint);
            GridViewColumnLayout.CoerceAutoColumns(Columns);
        }

        return desiredSize;
    }
}
