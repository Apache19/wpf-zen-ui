// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays determinate progress as a circular arc or continuous activity when
/// <see cref="IsIndeterminate"/> is enabled.
/// </summary>
/// <remarks>
/// The default template supplies <c>PART_DeterminateArc</c>. A custom template may omit the part;
/// range properties continue to work, but no determinate geometry is produced.
/// </remarks>
[TemplatePart(Name = DeterminateArcPartName, Type = typeof(Arc))]
public class ProgressRing : Control
{
    private const string DeterminateArcPartName = "PART_DeterminateArc";
    private Arc? determinateArc;

    #region DP: Minimum

    /// <summary>Identifies the <see cref="Minimum"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(0.0d, OnMinimumChanged),
        ValidateFiniteDouble
    );

    /// <summary>
    /// Gets or sets the lower bound of the determinate range. The default is <c>0</c>.
    /// Changing it coerces <see cref="Maximum"/> and <see cref="Value"/> to preserve a valid range.
    /// </summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    #endregion

    #region DP: Maximum

    /// <summary>Identifies the <see cref="Maximum"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(100.0d, OnMaximumChanged, CoerceMaximum),
        ValidateFiniteDouble
    );

    /// <summary>
    /// Gets or sets the upper bound of the determinate range. The default is <c>100</c>; values
    /// below <see cref="Minimum"/> are coerced to the minimum.
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    #endregion

    #region DP: Value

    /// <summary>Identifies the <see cref="Value"/> dependency property.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(
            0.0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged,
            CoerceValue
        ),
        ValidateFiniteDouble
    );

    /// <summary>
    /// Gets or sets the current determinate progress. The default is <c>0</c>; values outside
    /// <see cref="Minimum"/> and <see cref="Maximum"/> are coerced to the effective range.
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    #endregion

    #region DP: IsIndeterminate

    /// <summary>Identifies the <see cref="IsIndeterminate"/> dependency property.</summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(ProgressRing),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the ring displays continuous activity instead of
    /// the determinate <see cref="Value"/>. Switching modes does not replace the template visuals.
    /// </summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    #endregion

    #region DP: StrokeThickness

    /// <summary>Identifies the <see cref="StrokeThickness"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(ProgressRing),
        new PropertyMetadata(double.NaN),
        ValidateStrokeThickness
    );

    /// <summary>
    /// Gets or sets the stroke thickness of both the indicator and background ring. The default
    /// <see cref="double.NaN"/> derives a thickness equal to one eighth of the shorter arranged side.
    /// </summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    #endregion

    #region DP: StrokeLineCap

    /// <summary>Identifies the <see cref="StrokeLineCap"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeLineCapProperty = DependencyProperty.Register(
        nameof(StrokeLineCap),
        typeof(PenLineCap),
        typeof(ProgressRing),
        new PropertyMetadata(PenLineCap.Round),
        ValidateStrokeLineCap
    );

    /// <summary>
    /// Gets or sets the line cap used at both ends of the indicator and background arcs. The
    /// default is <see cref="PenLineCap.Round"/>.
    /// </summary>
    public PenLineCap StrokeLineCap
    {
        get => (PenLineCap)GetValue(StrokeLineCapProperty);
        set => SetValue(StrokeLineCapProperty, value);
    }

    #endregion

    static ProgressRing()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(typeof(ProgressRing))
        );
    }

    /// <summary>
    /// Resolves the optional determinate template part and synchronizes its derived geometry.
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        determinateArc = GetTemplateChild(DeterminateArcPartName) as Arc;
        UpdateProgressAngle();
    }

    /// <summary>
    /// Updates the template arc from the range properties. The angle is deliberately not exposed
    /// as another dependency property: it is derived state and allowing callers to write it would
    /// create two competing sources of truth for determinate progress.
    /// </summary>
    private void UpdateProgressAngle()
    {
        if (determinateArc is null)
        {
            return;
        }

        var range = Maximum - Minimum;
        var angle = range <= 0.0d ? 0.0d : 360.0d * ((Value - Minimum) / range);
        determinateArc.SetCurrentValue(Arc.EndAngleProperty, angle);
    }

    private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ProgressRing)d;
        control.CoerceValue(MaximumProperty);
        control.CoerceValue(ValueProperty);
        control.UpdateProgressAngle();
    }

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ProgressRing)d;
        control.CoerceValue(ValueProperty);
        control.UpdateProgressAngle();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ProgressRing)d).UpdateProgressAngle();
    }

    /// <summary>
    /// Maintains a non-negative range while preserving the requested base value for later coercion.
    /// </summary>
    private static object CoerceMaximum(DependencyObject d, object? value)
    {
        var control = (ProgressRing)d;
        return Math.Max(control.Minimum, (double)value!);
    }

    /// <summary>
    /// Constrains determinate progress to the effective range so geometry never needs a second
    /// clamping policy that could disagree with the public value.
    /// </summary>
    private static object CoerceValue(DependencyObject d, object? value)
    {
        var control = (ProgressRing)d;
        return Math.Max(control.Minimum, Math.Min(control.Maximum, (double)value!));
    }

    /// <summary>
    /// Rejects values that cannot define a finite range or WPF geometry.
    /// </summary>
    private static bool ValidateFiniteDouble(object? value)
    {
        var number = (double)value!;
        return !double.IsNaN(number) && !double.IsInfinity(number);
    }

    /// <summary>
    /// Preserves <see cref="double.NaN"/> as automatic sizing while rejecting invalid WPF pens.
    /// </summary>
    private static bool ValidateStrokeThickness(object? value)
    {
        var thickness = (double)value!;
        return double.IsNaN(thickness) || (!double.IsInfinity(thickness) && thickness >= 0.0d);
    }

    /// <summary>
    /// Prevents undefined enum values from reaching the Arc line-cap properties.
    /// </summary>
    private static bool ValidateStrokeLineCap(object? value) =>
        value is not null && Enum.IsDefined(typeof(PenLineCap), value);
}
