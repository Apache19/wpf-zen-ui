// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using NativeScrollViewer = System.Windows.Controls.ScrollViewer;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Presents a GridView row and coordinates Auto, pixel, and star-sized columns.
/// </summary>
public class GridViewRowPresenter : System.Windows.Controls.GridViewRowPresenter
{
    private NativeScrollViewer? _scrollViewer;
    private bool _wasMeasuredWithFiniteWidth;

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (TryGetStarLayoutWidth(arrangeSize, out double availableWidth))
        {
            _ = GridViewColumnLayout.ResolveStarColumns(Columns, availableWidth);
        }

        return base.ArrangeOverride(arrangeSize);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _wasMeasuredWithFiniteWidth = GridViewColumnLayout.HasFiniteWidth(constraint.Width);

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

    private bool TryGetStarLayoutWidth(Size arrangeSize, out double availableWidth)
    {
        NativeScrollViewer? scrollViewer = GetScrollViewer();
        if (
            scrollViewer is not null
         && GridViewColumnLayout.HasFiniteWidth(scrollViewer.ViewportWidth)
         && scrollViewer.ViewportWidth > 0d
        )
        {
            availableWidth = scrollViewer.ViewportWidth;
            return true;
        }

        // When no scrolling host exists, a finite Measure constraint establishes an independent
        // presenter's layout width. An Arrange width following an infinite Measure is content-derived
        // and cannot safely be used for Star allocation because the columns contribute to that width.
        availableWidth = arrangeSize.Width;
        return _wasMeasuredWithFiniteWidth && GridViewColumnLayout.HasFiniteWidth(availableWidth);
    }

    private NativeScrollViewer? GetScrollViewer()
    {
        if (_scrollViewer?.IsAncestorOf(this) == true)
        {
            return _scrollViewer;
        }

        // GridViewRowPresenter is inside the item container rather than the ScrollViewer template,
        // so it has no useful TemplatedParent link to the viewport. Walking visual ancestors mirrors
        // WPF's internal GridViewRowPresenter.FindViewPort logic without reflecting ItemsControl.ScrollHost.
        for (
            DependencyObject? parent = VisualTreeHelper.GetParent(this);
            parent is not null;
            parent = VisualTreeHelper.GetParent(parent)
        )
        {
            if (parent is NativeScrollViewer scrollViewer)
            {
                _scrollViewer = scrollViewer;
                return scrollViewer;
            }
        }

        _scrollViewer = null;
        return null;
    }
}
