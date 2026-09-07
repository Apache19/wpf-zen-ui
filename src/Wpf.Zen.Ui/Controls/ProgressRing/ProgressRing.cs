// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays determinate progress as a circular indicator or continuous activity when
/// <see cref="IsIndeterminate"/> is enabled.
/// </summary>
/// <remarks>
/// <para>
/// The range contract — <see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/>,
/// <see cref="RangeBase.Value"/>, their coercion and the <see cref="RangeBase.ValueChanged"/>
/// event — is inherited from <see cref="RangeBase"/>, matching the native
/// <see cref="ProgressBar"/> pattern.
/// </para>
/// <para>
/// The ring is rendered by the control itself. Its template intentionally has no geometry parts,
/// so custom templates cannot accidentally change the progress calculation, circular layout or
/// animation contract. <see cref="Control.Foreground"/> colors the indicator and
/// <see cref="Control.Background"/> colors the optional full-circle track.
/// </para>
/// </remarks>
public class ProgressRing : RangeBase
{
    private const double DefaultIndeterminateSweepAngle = 100.0d;
    private const double MaximumIndeterminateSweepAngle = 320.0d;

    #region DP: IsIndeterminate

    /// <summary>Identifies the <see cref="IsIndeterminate"/> dependency property.</summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnIsIndeterminateChanged
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the ring displays continuous activity instead of
    /// the determinate <see cref="RangeBase.Value"/>. The default is <see langword="false"/>.
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
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateStrokeThickness
    );

    /// <summary>
    /// Gets or sets the stroke thickness of both the indicator and background ring. The default
    /// <see cref="double.NaN"/> derives a thickness equal to one eighth of the shorter rendered side.
    /// Changing this property redraws the ring without requiring a layout pass.
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
        new FrameworkPropertyMetadata(
            PenLineCap.Round,
            FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateStrokeLineCap
    );

    /// <summary>
    /// Gets or sets the line cap used at both ends of a partial indicator arc. The default is
    /// <see cref="PenLineCap.Round"/>. A complete ring has no endpoints, so this property does not
    /// affect the appearance at 100 percent.
    /// </summary>
    public PenLineCap StrokeLineCap
    {
        get => (PenLineCap)GetValue(StrokeLineCapProperty);
        set => SetValue(StrokeLineCapProperty, value);
    }

    #endregion

    #region Private animation properties

    private static readonly DependencyProperty IndeterminateRotationProperty =
        DependencyProperty.Register(
            nameof(IndeterminateRotation),
            typeof(double),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(
                0.0d,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the clockwise rotation applied to the indeterminate indicator. This private
    /// dependency-property wrapper exists only because WPF animations target dependency properties.
    /// </summary>
    private double IndeterminateRotation
    {
        get => (double)GetValue(IndeterminateRotationProperty);
        set => SetValue(IndeterminateRotationProperty, value);
    }

    private static readonly DependencyProperty IndeterminateSweepAngleProperty =
        DependencyProperty.Register(
            nameof(IndeterminateSweepAngle),
            typeof(double),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(
                DefaultIndeterminateSweepAngle,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the animated angular length of the indeterminate indicator. It remains private
    /// because it is transient rendering state rather than part of the control's value contract.
    /// </summary>
    private double IndeterminateSweepAngle
    {
        get => (double)GetValue(IndeterminateSweepAngleProperty);
        set => SetValue(IndeterminateSweepAngleProperty, value);
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

        IsTabStopProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(false)
        );

        // Control defines these brushes for template consumption and therefore does not mark them
        // as render-affecting. ProgressRing consumes them directly in OnRender, so their metadata
        // must invalidate the retained drawing when a brush reference changes.

        BackgroundProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        ForegroundProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(
                SystemColors.ControlTextBrush,
                FrameworkPropertyMetadataOptions.Inherits |
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressRing"/> class.
    /// </summary>
    public ProgressRing()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        InvalidateVisual();
    }

    /// <summary>
    /// Draws the track and current indicator in the largest centered circle that fits within the
    /// rendered bounds. Drawing here makes the circular geometry independent of template layout.
    /// </summary>
    /// <param name="drawingContext">The drawing instructions for this control.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var diameter = Math.Min(RenderSize.Width, RenderSize.Height);
        var strokeThickness = double.IsNaN(StrokeThickness)
            ? diameter / 8.0d
            : StrokeThickness;

        if (diameter <= 0.0d || strokeThickness <= 0.0d)
        {
            return;
        }

        // Centering against RenderSize, rather than arranging a square template child, keeps a
        // ring circular and centered even when its parent assigns rectangular bounds.
        var center = new Point(RenderSize.Width / 2.0d, RenderSize.Height / 2.0d);
        var radius = Math.Max(0.0d, (diameter - strokeThickness) / 2.0d);

        DrawFullRing(drawingContext, Background, center, radius, strokeThickness);

        var sweepAngle = IsIndeterminate
            ? IndeterminateSweepAngle
            : GetDeterminateSweepAngle();

        if (sweepAngle <= 0.0d || Foreground is null)
        {
            return;
        }

        if (sweepAngle >= 360.0d)
        {
            DrawFullRing(drawingContext, Foreground, center, radius, strokeThickness);
            return;
        }

        var startAngle = IsIndeterminate ? IndeterminateRotation : 0.0d;
        DrawArc(
            drawingContext,
            Foreground,
            center,
            radius,
            strokeThickness,
            startAngle,
            sweepAngle
        );
    }

    /// <summary>
    /// Draws a complete track without manufacturing a nearly-complete arc whose coincident
    /// endpoints are ambiguous to WPF's arc geometry.
    /// </summary>
    private static void DrawFullRing(
        DrawingContext drawingContext,
        Brush?         brush,
        Point          center,
        double         radius,
        double         strokeThickness)
    {
        if (brush is null)
        {
            return;
        }

        drawingContext.DrawEllipse(
            null,
            CreatePen(brush, strokeThickness, PenLineCap.Flat),
            center,
            radius,
            radius
        );
    }

    /// <summary>
    /// Builds only the open centerline consumed by the pen. Fill geometry is intentionally absent:
    /// a progress ring represents a stroked interval, not a sector or closed shape.
    /// </summary>
    private void DrawArc(
        DrawingContext drawingContext,
        Brush          brush,
        Point          center,
        double         radius,
        double         strokeThickness,
        double         startAngle,
        double         sweepAngle)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(PointAtAngle(center, radius, startAngle), false, false);
            context.ArcTo(
                PointAtAngle(center, radius, startAngle + sweepAngle),
                new Size(radius, radius),
                0.0d,
                sweepAngle > 180.0d,
                SweepDirection.Clockwise,
                true,
                false
            );
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(
            null,
            CreatePen(brush, strokeThickness, StrokeLineCap),
            geometry
        );
    }

    /// <summary>
    /// Converts a clockwise angle measured from twelve o'clock into WPF device coordinates.
    /// </summary>
    private static Point PointAtAngle(Point center, double radius, double angle)
    {
        var radians = angle * (Math.PI / 180.0d);
        return new Point(
            center.X + (radius * Math.Sin(radians)),
            center.Y - (radius * Math.Cos(radians))
        );
    }

    /// <summary>
    /// Creates the pen shared by full and partial ring rendering. Freezing is conditional because
    /// a caller may supply an animated brush, which cannot itself be frozen.
    /// </summary>
    private static Pen CreatePen(Brush brush, double thickness, PenLineCap lineCap)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = lineCap,
            EndLineCap = lineCap,
        };

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    /// <summary>
    /// Maps the inherited range to one clockwise revolution. RangeBase coercion guarantees that
    /// the result remains between zero and one when the range has a positive length.
    /// </summary>
    private double GetDeterminateSweepAngle()
    {
        var range = Maximum - Minimum;
        return range <= 0.0d ? 0.0d : 360.0d * ((Value - Minimum) / range);
    }

    /// <summary>
    /// Starts both independent indeterminate phases. Rotation establishes continuous movement,
    /// while the changing sweep prevents the indicator from appearing mechanically static.
    /// </summary>
    private void StartIndeterminateAnimation()
    {
        var rotationAnimation = new DoubleAnimation(0.0d, 360.0d, TimeSpan.FromSeconds(2.0d))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        var sweepAnimation = new DoubleAnimation(
            DefaultIndeterminateSweepAngle,
            MaximumIndeterminateSweepAngle,
            TimeSpan.FromSeconds(5.0d)
        )
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };

        BeginAnimation(
            IndeterminateRotationProperty,
            rotationAnimation,
            HandoffBehavior.SnapshotAndReplace
        );

        BeginAnimation(
            IndeterminateSweepAngleProperty,
            sweepAnimation,
            HandoffBehavior.SnapshotAndReplace
        );
    }

    /// <summary>
    /// Removes animation clocks and restores stable starting values. Stopping clocks while the
    /// control is unloaded prevents detached rings from consuming composition ticks.
    /// </summary>
    private void StopIndeterminateAnimation()
    {
        BeginAnimation(IndeterminateRotationProperty, null);
        BeginAnimation(IndeterminateSweepAngleProperty, null);

        IndeterminateRotation = 0.0d;
        IndeterminateSweepAngle = DefaultIndeterminateSweepAngle;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsIndeterminate)
        {
            StartIndeterminateAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopIndeterminateAnimation();
    }

    private static void OnIsIndeterminateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var progressRing = (ProgressRing)d;

        if ((bool)e.NewValue && progressRing.IsLoaded)
        {
            progressRing.StartIndeterminateAnimation();
        }
        else
        {
            progressRing.StopIndeterminateAnimation();
        }
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
    /// Prevents undefined enum values from reaching the WPF pen used during rendering.
    /// </summary>
    private static bool ValidateStrokeLineCap(object? value)
    {
        return value is not null && Enum.IsDefined(typeof(PenLineCap), value);
    }
}
