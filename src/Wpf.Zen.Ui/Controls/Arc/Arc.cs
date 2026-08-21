// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Windows.Shapes;

using Point = System.Windows.Point;
using Size = System.Windows.Size;

// ReSharper disable CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Draws an elliptical arc between two angles.
/// Angles are measured clockwise from the top of the arranged bounds; <see cref="SweepDirection"/>
/// selects which of the two possible paths connects the endpoints.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:Arc
///     EndAngle="359"
///     StartAngle="0"
///     Stroke="{ui:DynamicBrush AccentStroke}"
///     StrokeThickness="2"
///     Visibility="Visible" /&gt;
/// </code>
/// </example>
public class Arc : Shape
{
    private static readonly Action<Shape>? ResetRenderedGeometry =
        CreateResetRenderedGeometryDelegate();

    #region DP: StartAngle

    /// <summary>Identifies the <see cref="StartAngle"/> dependency property.</summary>
    public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register(
        nameof(StartAngle),
        typeof(double),
        typeof(Arc),
        new FrameworkPropertyMetadata(
            0.0d,
            FrameworkPropertyMetadataOptions.AffectsRender,
            PropertyChangedCallback
        ),
        ValidateFiniteAngle
    );

    /// <summary>
    /// Gets or sets the arc's starting angle in degrees, measured clockwise from the top of its
    /// arranged bounds. The default is <c>0</c>; the value must be finite and supports animation.
    /// </summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    #endregion

    #region DP: EndAngle

    /// <summary>Identifies the <see cref="EndAngle"/> dependency property.</summary>
    public static readonly DependencyProperty EndAngleProperty = DependencyProperty.Register(
        nameof(EndAngle),
        typeof(double),
        typeof(Arc),
        new FrameworkPropertyMetadata(
            0.0d,
            FrameworkPropertyMetadataOptions.AffectsRender,
            PropertyChangedCallback
        ),
        ValidateFiniteAngle
    );

    /// <summary>
    /// Gets or sets the arc's ending angle in degrees, measured clockwise from the top of its
    /// arranged bounds. The default is <c>0</c>; a difference of at least one revolution draws a
    /// complete ellipse. The value must be finite and supports animation.
    /// </summary>
    public double EndAngle
    {
        get => (double)GetValue(EndAngleProperty);
        set => SetValue(EndAngleProperty, value);
    }

    #endregion

    #region DP: SweepDirection

    /// <summary>Identifies the <see cref="SweepDirection"/> dependency property.</summary>
    public static readonly DependencyProperty SweepDirectionProperty = DependencyProperty.Register(
        nameof(SweepDirection),
        typeof(SweepDirection),
        typeof(Arc),
        new FrameworkPropertyMetadata(
            SweepDirection.Clockwise,
            FrameworkPropertyMetadataOptions.AffectsRender,
            PropertyChangedCallback
        ),
        ValidateSweepDirection
    );

    /// <summary>
    /// Gets or sets the path direction between <see cref="StartAngle"/> and <see cref="EndAngle"/>.
    /// The default is <see cref="SweepDirection.Clockwise"/>. Changing the direction selects the
    /// complementary path without changing either physical endpoint.
    /// </summary>
    public SweepDirection SweepDirection
    {
        get => (SweepDirection)GetValue(SweepDirectionProperty);
        set => SetValue(SweepDirectionProperty, value);
    }

    #endregion

    /// <inheritdoc />
    protected override Geometry DefiningGeometry => CreateGeometry();

    /// <summary>
    /// Get the geometry that defines this shape.
    /// <para><see href="https://stackoverflow.com/a/36756365/13224348">Based on Mark Feldman implementation.</see></para>
    /// </summary>
    private Geometry CreateGeometry()
    {
        var sweepAngle = GetSweepAngle();

        // Draw a full ellipse when the sweep angle covers a complete circle,
        // avoiding the degenerate arc that occurs when start and end angles coincide.
        if (sweepAngle >= 360.0d)
        {
            var radiusX = Math.Max(0, (RenderSize.Width - StrokeThickness) / 2);
            var radiusY = Math.Max(0, (RenderSize.Height - StrokeThickness) / 2);

            return new EllipseGeometry(
                new Rect(
                    StrokeThickness / 2,
                    StrokeThickness / 2,
                    radiusX * 2,
                    radiusY * 2
                )
            );
        }

        var geometryStream = new StreamGeometry();
        var arcSize = new Size(
            Math.Max(0, (RenderSize.Width - StrokeThickness) / 2),
            Math.Max(0, (RenderSize.Height - StrokeThickness) / 2)
        );

        using var context = geometryStream.Open();
        context.BeginFigure(PointAtAngle(StartAngle), false, false);

        context.ArcTo(
            PointAtAngle(EndAngle),
            arcSize,
            0,
            sweepAngle > 180.0d,
            SweepDirection,
            true,
            false
        );

        geometryStream.Transform = new TranslateTransform(StrokeThickness / 2, StrokeThickness / 2);

        return geometryStream;
    }

    /// <summary>
    /// Draws a point on the coordinates of the given angle.
    /// <para><see href="https://stackoverflow.com/a/36756365/13224348">Based on Mark Feldman implementation.</see></para>
    /// </summary>
    /// <param name="angle">The angle at which to create the point.</param>
    private Point PointAtAngle(double angle)
    {
        var radians = angle * (Math.PI / 180.0d);
        var xRadius = Math.Max(0.0d, (RenderSize.Width - StrokeThickness) / 2.0d);
        var yRadius = Math.Max(0.0d, (RenderSize.Height - StrokeThickness) / 2.0d);

        // Screen coordinates grow downward, so sine controls X while negative cosine controls Y.
        // Keeping this conversion independent of SweepDirection ensures the two angle properties
        // always identify the same physical endpoints; direction only chooses the connecting path.
        return new Point(
            xRadius + (xRadius * Math.Sin(radians)),
            yRadius - (yRadius * Math.Cos(radians))
        );
    }

    /// <summary>
    /// Calculates the directed sweep while preserving explicit complete revolutions.
    /// </summary>
    private double GetSweepAngle()
    {
        var directedDifference = SweepDirection == SweepDirection.Clockwise
            ? EndAngle - StartAngle
            : StartAngle - EndAngle;

        // An endpoint one or more revolutions away explicitly requests a complete ellipse. For
        // smaller differences, normalize to [0, 360) so direction selects the short or long route
        // without reordering the caller's endpoints.
        if (Math.Abs(directedDifference) >= 360.0d)
        {
            return 360.0d;
        }

        var normalized = directedDifference % 360.0d;
        return normalized < 0.0d ? normalized + 360.0d : normalized;
    }

    /// <summary>
    /// Event triggered when one of the key parameters is changed. Forces the geometry to be redrawn.
    /// </summary>
    protected static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Arc control)
        {
            return;
        }

        // Shape caches the result of DefiningGeometry in a private render cache. InvalidateVisual
        // alone redraws that cached instance, so animated angles would not reliably create a new
        // path. WPF exposes no public invalidation hook for custom Shape geometry; use its internal
        // reset when available and fall back to layout, whose Shape.ArrangeOverride performs the
        // same reset. The delegate is cached because this callback runs on every animation tick.
        if (ResetRenderedGeometry is null)
        {
            control.InvalidateArrange();
        }
        else
        {
            ResetRenderedGeometry(control);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        // DefiningGeometry is derived from RenderSize and therefore has no independent natural
        // size. Shape.MeasureOverride asks that geometry for a natural size when Stretch is None,
        // which feeds the previous RenderSize back as DesiredSize and prevents a parent from
        // shrinking the Arc. Accept finite constraints and remain empty on unconstrained axes.
        return new Size(
            double.IsInfinity(constraint.Width) ? 0.0d : Math.Max(0.0d, constraint.Width),
            double.IsInfinity(constraint.Height) ? 0.0d : Math.Max(0.0d, constraint.Height)
        );
    }

    /// <summary>
    /// Creates the cached accessor required to invalidate WPF's private Shape geometry cache.
    /// </summary>
    /// <remarks>
    /// Failure is tolerated because <see cref="PropertyChangedCallback"/> falls back to an arrange
    /// invalidation. This keeps Arc functional if the internal method changes in a future WPF build.
    /// </remarks>
    private static Action<Shape>? CreateResetRenderedGeometryDelegate()
    {
        try
        {
            var method = typeof(Shape).GetMethod(
                "ResetRenderedGeometry",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (method is null)
            {
                return null;
            }

            // A normal delegate cannot cross the assembly boundary to this internal WPF method.
            // A cached DynamicMethod pays the visibility cost once and keeps animation ticks free
            // from MethodInfo.Invoke allocations.
            var accessor = new DynamicMethod(
                "ResetShapeRenderedGeometry",
                typeof(void),
                [typeof(Shape)],
                typeof(Arc),
                skipVisibility: true
            );
            var generator = accessor.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, method);
            generator.Emit(OpCodes.Ret);

            return (Action<Shape>)accessor.CreateDelegate(typeof(Action<Shape>));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (MethodAccessException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rejects values that WPF geometry cannot represent as coordinates.
    /// </summary>
    private static bool ValidateFiniteAngle(object? value)
    {
        var angle = (double)value!;

        return !double.IsNaN(angle) && !double.IsInfinity(angle);
    }

    /// <summary>
    /// Prevents undefined values from reaching <see cref="StreamGeometryContext.ArcTo"/>.
    /// </summary>
    private static bool ValidateSweepDirection(object? value)
    {
        return value is not null && Enum.IsDefined(typeof(SweepDirection), value);
    }
}
