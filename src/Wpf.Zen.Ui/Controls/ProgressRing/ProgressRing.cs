// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays determinate progress as a circular arc or continuous activity when
/// <see cref="IsIndeterminate"/> is enabled.
/// </summary>
/// <remarks>
/// The range contract — <see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/>,
/// <see cref="RangeBase.Value"/>, their coercion/validation and the <see cref="RangeBase.ValueChanged"/>
/// event — is inherited from <see cref="RangeBase"/>, matching the native <see cref="ProgressBar"/>
/// pattern. The default template supplies <c>PART_DeterminateArc</c>. A custom template may omit the
/// part; range properties continue to work, but no determinate geometry is produced.
/// </remarks>
[TemplatePart(Name = DeterminateArcPartName, Type = typeof(Arc))]
public class ProgressRing : RangeBase
{
    private const string DeterminateArcPartName = "PART_DeterminateArc";
    private Arc? determinateArc;

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
    /// the determinate <see cref="RangeBase.Value"/>. Switching modes does not replace the template visuals.
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
        // RangeBase defaults Maximum to 1, but a progress indicator expresses percent by default.
        // The native ProgressBar applies the same override; coercion and change callbacks from
        // RangeBase are preserved.
        RangeBase.MaximumProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(100.0d)
        );

        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(typeof(ProgressRing))
        );

        FocusableProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(false)
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

    /// <inheritdoc />
    protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        UpdateProgressAngle();
    }

    /// <inheritdoc />
    protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        UpdateProgressAngle();
    }

    /// <inheritdoc />
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
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
    private static bool ValidateStrokeLineCap(object? value)
    {
        return value is not null && Enum.IsDefined(typeof(PenLineCap), value);
    }
}
