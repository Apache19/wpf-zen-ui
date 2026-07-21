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

    private static void OnColumnReorderIndicatorTemplateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((GridViewHeaderRowPresenter)d).UpdateIndicatorStyle();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateIndicatorStyle();
    }

    private void UpdateIndicatorStyle()
    {
        if (
            ColumnReorderIndicatorTemplate is not { } template
            || _indicatorField.Value?.GetValue(this) is not Separator indicator
        )
        {
            return;
        }

        indicator.Margin = new Thickness(0);
        indicator.Width = 3.0;
        indicator.Template = template;
    }
}
