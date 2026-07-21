// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using NativeGridViewHeaderRowPresenter = System.Windows.Controls.GridViewHeaderRowPresenter;

namespace Wpf.Ui.Controls;

/// <summary>
/// Presents GridView headers and coordinates Auto, pixel, and star-sized columns.
/// </summary>
public class GridViewHeaderRowPresenter : NativeGridViewHeaderRowPresenter
{
    private static readonly Lazy<FieldInfo?> _indicatorField = new(static () =>
        typeof(NativeGridViewHeaderRowPresenter).GetField(
            "_indicator",
            BindingFlags.NonPublic | BindingFlags.Instance
        )
    );

    private static bool _isIndicatorAccessAvailable = true;

    private ScrollViewer? _starLayoutScrollViewer;
    private bool _wasMeasuredWithFiniteWidth;

    /// <summary>
    /// Identifies the <see cref="ColumnReorderIndicatorTemplate"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnReorderIndicatorTemplateProperty =
        DependencyProperty.Register(
            nameof(ColumnReorderIndicatorTemplate),
            typeof(ControlTemplate),
            typeof(GridViewHeaderRowPresenter),
            new FrameworkPropertyMetadata(null, OnColumnReorderIndicatorTemplateChanged)
        );

    public GridViewHeaderRowPresenter()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Gets or sets the template used for the column reorder insertion indicator.
    /// </summary>
    public ControlTemplate? ColumnReorderIndicatorTemplate
    {
        get => (ControlTemplate?)GetValue(ColumnReorderIndicatorTemplateProperty);
        set => SetValue(ColumnReorderIndicatorTemplateProperty, value);
    }

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
        // TemplatedParent is available during template layout, before Loaded. Subscribe here so the
        // first viewport publication cannot race ahead of the Loaded event.
        AttachStarLayoutScrollViewer();
        _wasMeasuredWithFiniteWidth = GridViewColumnLayout.HasFiniteWidth(constraint.Width);

        // WPF hosts the header in a horizontally enabled child ScrollViewer, so it normally receives
        // an infinite measure constraint. Rows can also be measured this way before a finite arrange
        // resolves Star columns; the header must preserve the resulting shared native widths.
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
        // In WPF's GridView template, TemplatedParent is the main ScrollViewer even though the
        // header's immediate parent is a second horizontally enabled ScrollViewer. That child host
        // measures the header with infinity and then arranges it to its content width. Using that
        // arrange width for Star allocation creates positive feedback: increasing a Star column
        // increases the next arrange width, which increases the column again. The main viewport is
        // the finite width Star columns are intended to fill and remains stable across layout passes.
        if (
            TemplatedParent is ScrollViewer scrollViewer
         && GridViewColumnLayout.HasFiniteWidth(scrollViewer.ViewportWidth)
         && scrollViewer.ViewportWidth > 0d
        )
        {
            availableWidth = scrollViewer.ViewportWidth;
            return true;
        }

        // A presenter used outside the standard scrolling template has no viewport to consult. Its
        // arrange width is authoritative only when Measure was finite; after an infinite Measure it
        // is content-derived and therefore must never feed back into Star allocation.
        availableWidth = arrangeSize.Width;
        return _wasMeasuredWithFiniteWidth && GridViewColumnLayout.HasFiniteWidth(availableWidth);
    }

    private static void OnColumnReorderIndicatorTemplateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((GridViewHeaderRowPresenter)d).UpdateIndicatorStyle();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachStarLayoutScrollViewer();
        ResolveStarColumnsFromViewport();
        UpdateIndicatorStyle();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_starLayoutScrollViewer is not null)
        {
            _starLayoutScrollViewer.ScrollChanged -= OnStarLayoutScrollChanged;
            _starLayoutScrollViewer = null;
        }
    }

    private void AttachStarLayoutScrollViewer()
    {
        var scrollViewer = TemplatedParent as ScrollViewer;
        if (ReferenceEquals(scrollViewer, _starLayoutScrollViewer))
        {
            return;
        }

        if (_starLayoutScrollViewer is not null)
        {
            _starLayoutScrollViewer.ScrollChanged -= OnStarLayoutScrollChanged;
        }

        _starLayoutScrollViewer = scrollViewer;
        if (_starLayoutScrollViewer is not null)
        {
            // During the first Arrange, ScrollViewer may not have published its viewport yet. Its
            // subsequent ScrollChanged notification includes viewport-size changes even when the
            // offset remains zero, providing the stable width needed to resolve Star columns. One
            // header presenter owns this subscription; virtualized row presenters remain passive.
            _starLayoutScrollViewer.ScrollChanged += OnStarLayoutScrollChanged;
        }
    }

    private void OnStarLayoutScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!AreClose(e.ViewportWidthChange, 0d))
        {
            ResolveStarColumnsFromViewport();
        }
    }

    private void ResolveStarColumnsFromViewport()
    {
        if (
            _starLayoutScrollViewer is not { ViewportWidth: > 0d } scrollViewer
            || !GridViewColumnLayout.HasFiniteWidth(scrollViewer.ViewportWidth)
        )
        {
            return;
        }

        _ = GridViewColumnLayout.ResolveStarColumns(Columns, scrollViewer.ViewportWidth);
    }

    private void UpdateIndicatorStyle()
    {
        if (ColumnReorderIndicatorTemplate is not { } template || !TryGetReorderIndicator(out var indicator))
        {
            return;
        }

        indicator.Margin = new Thickness(0);
        indicator.Width = 3.0;
        indicator.Template = template;
    }

    private bool TryGetReorderIndicator(out Separator indicator)
    {
        indicator = null!;
        if (!_isIndicatorAccessAvailable || _indicatorField.Value is not { } indicatorField)
        {
            _isIndicatorAccessAvailable = false;
            return false;
        }

        try
        {
            if (indicatorField.GetValue(this) is not Separator nativeIndicator)
            {
                // A null indicator merely means WPF has not started a drag. Keep reflection enabled
                // so the same presenter can apply the template when a later drag creates it.
                return false;
            }

            indicator = nativeIndicator;
            return true;
        }
        catch (Exception)
        {
            // Column reordering remains fully functional with WPF's native indicator if an internal
            // member changes or becomes inaccessible; only the Fluent indicator styling is skipped.
            _isIndicatorAccessAvailable = false;
            return false;
        }
    }

    private static bool AreClose(double first, double second)
    {
        return Math.Abs(first - second) < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1d);
    }
}
