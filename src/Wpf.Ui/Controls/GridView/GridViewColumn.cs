// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;

namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a <see cref="GridView"/> column whose width can be Auto, pixel, or star sized.
/// </summary>
/// <remarks>
/// Auto columns share the widest measured header or visible cell, just like the native WPF
/// <see cref="NativeGridViewColumn"/>. Star columns divide the remaining row width by weight.
/// Dragging a header gripper converts the column to a pixel width; double-clicking it restores Auto.
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:GridView&gt;
///     &lt;ui:GridViewColumn Width="Auto" Header="Name" DisplayMemberBinding="{Binding Name}" /&gt;
///     &lt;ui:GridViewColumn Width="2*" Header="Description" DisplayMemberBinding="{Binding Description}" /&gt;
///     &lt;ui:GridViewColumn Width="120" Header="Status" DisplayMemberBinding="{Binding Status}" /&gt;
/// &lt;/ui:GridView&gt;
/// </code>
/// </example>
public class GridViewColumn : NativeGridViewColumn
{
    private static readonly Lazy<FieldInfo> _desiredWidthField = new(static () =>
        typeof(NativeGridViewColumn).GetField("_desiredWidth", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("The native GridViewColumn._desiredWidth field was not found.")
    );

    private static readonly Lazy<MethodInfo> _updateActualWidthMethod = new(static () =>
        typeof(NativeGridViewColumn).GetMethod(
            "UpdateActualWidth",
            BindingFlags.NonPublic | BindingFlags.Instance
        )
        ?? throw new InvalidOperationException(
            "The native GridViewColumn.UpdateActualWidth method was not found."
        )
    );

    private bool _isSynchronizingNativeWidth;

    static GridViewColumn()
    {
        NativeGridViewColumn.WidthProperty.OverrideMetadata(
            typeof(GridViewColumn),
            new PropertyMetadata(double.NaN, OnWidthChanged)
        );
    }

    /// <summary>
    /// Identifies the <see cref="Width"/> dependency property.
    /// </summary>
    public static new readonly DependencyProperty WidthProperty = DependencyProperty.Register(
        nameof(Width),
        typeof(GridLength),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(GridLength.Auto, OnWidthChanged)
    );

    /// <summary>
    /// Identifies the <see cref="MinWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(
        nameof(MinWidth),
        typeof(double),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(0.0, OnMinWidthChanged),
        ValidateMinWidth
    );

    /// <summary>
    /// Identifies the <see cref="MaxWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(
        nameof(MaxWidth),
        typeof(double),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(double.PositiveInfinity, OnMaxWidthChanged, CoerceMaxWidth),
        ValidateMaxWidth
    );

    /// <summary>
    /// Gets or sets the column width. The default is <see cref="GridLength.Auto"/>.
    /// </summary>
    public new GridLength Width
    {
        get => (GridLength)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum rendered width of the column.
    /// </summary>
    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum rendered width of the column.
    /// </summary>
    public double MaxWidth
    {
        get => (double)GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    internal double NativeWidth => (double)GetValue(NativeGridViewColumn.WidthProperty);

    internal double CoerceWidth(double value)
    {
        return Math.Max(MinWidth, Math.Min(value, MaxWidth));
    }

    internal void PrepareForMeasure(bool hasFiniteWidth)
    {
        if (Width.IsAuto)
        {
            SetNativeWidth(double.NaN);
            return;
        }

        if (Width.IsAbsolute)
        {
            SetNativeWidth(CoerceWidth(Width.Value));
            return;
        }

        if (!hasFiniteWidth)
        {
            // Grid treats star definitions as Auto when measured with infinite space.
            SetNativeWidth(double.NaN);
        }
        else if (double.IsNaN(NativeWidth))
        {
            SetNativeWidth(CoerceWidth(MinWidth));
        }
    }

    internal bool SetStarActualWidth(double value)
    {
        double coercedWidth = CoerceWidth(value);
        if (AreClose(NativeWidth, coercedWidth))
        {
            return false;
        }

        SetNativeWidth(coercedWidth);
        return true;
    }

    internal void CoerceAutoDesiredWidth()
    {
        if (!Width.IsAuto && !(Width.IsStar && double.IsNaN(NativeWidth)))
        {
            return;
        }

        double desiredWidth = (double)(
            _desiredWidthField.Value.GetValue(this)
            ?? throw new InvalidOperationException("Failed to read the native GridViewColumn desired width.")
        );
        double coercedWidth = CoerceWidth(desiredWidth);

        if (AreClose(desiredWidth, coercedWidth))
        {
            return;
        }

        _desiredWidthField.Value.SetValue(this, coercedWidth);
        _ = _updateActualWidthMethod.Value.Invoke(this, null);
    }

    private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        if (e.Property == WidthProperty)
        {
            column.ResetNativeWidthForDefinition();
            return;
        }

        if (column._isSynchronizingNativeWidth)
        {
            return;
        }

#pragma warning disable WPF0020, WPF0022 // The derived Width DP intentionally replaces the native double DP with GridLength.
        double width = (double)e.NewValue;
        column.SetCurrentValue(
            WidthProperty,
            double.IsNaN(width) ? GridLength.Auto : new GridLength(column.CoerceWidth(width))
        );
#pragma warning restore WPF0020, WPF0022
    }

    private static void OnMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        column.CoerceValue(MaxWidthProperty);
        column.CoerceAutoDesiredWidth();
        column.ResetNativeWidthForDefinition();
    }

    private static void OnMaxWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        column.CoerceAutoDesiredWidth();
        column.ResetNativeWidthForDefinition();
    }

    private static object? CoerceMaxWidth(DependencyObject d, object? baseValue)
    {
        var column = (GridViewColumn)d;
        return Math.Max(column.MinWidth, (double)baseValue!);
    }

    private void ResetNativeWidthForDefinition()
    {
        // Force the native presenters to observe star-weight and constraint-only changes.
        if (!double.IsNaN(NativeWidth))
        {
            SetNativeWidth(double.NaN);
        }

        if (Width.IsAbsolute)
        {
            SetNativeWidth(CoerceWidth(Width.Value));
        }
        else if (Width.IsStar)
        {
            SetNativeWidth(CoerceWidth(MinWidth));
        }
    }

    private void SetNativeWidth(double value)
    {
        if (AreClose(NativeWidth, value))
        {
            return;
        }

        _isSynchronizingNativeWidth = true;
        try
        {
            SetCurrentValue(NativeGridViewColumn.WidthProperty, value);
        }
        finally
        {
            _isSynchronizingNativeWidth = false;
        }
    }

    private static bool ValidateMinWidth(object? value)
    {
        return value is double width && !double.IsNaN(width) && !double.IsInfinity(width) && width >= 0.0;
    }

    private static bool ValidateMaxWidth(object? value)
    {
        return value is double width && !double.IsNaN(width) && width >= 0.0;
    }

    private static bool AreClose(double first, double second)
    {
        if (double.IsNaN(first))
        {
            return double.IsNaN(second);
        }

        if (double.IsInfinity(first) || double.IsInfinity(second))
        {
            return first.Equals(second);
        }

        double delta = Math.Abs(first - second);
        return delta < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1.0);
    }
}
