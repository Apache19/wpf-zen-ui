// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Creates a one-way binding that calculates pill corners from an element's actual dimensions.
/// </summary>
/// <remarks>
/// <para>
/// Apply directly to a dependency property: <c>CornerRadius="{conv:PillCornerConverter}"</c>.
/// The source defaults to the target itself. WPF observes its ActualWidth and ActualHeight,
/// including layout and animation changes. Styles and control templates are supported.
/// </para>
/// <para>
/// Use <c>Corners=Left</c> or <c>Corners='TopLeft,BottomRight'</c> to select physical corners;
/// unselected corners remain square. Use <c>Dimension=Height</c> for horizontal segments
/// that share a height but have different widths. ElementName or RelativeSource can instead
/// select a common container, including the templated parent.
/// </para>
/// <para>
/// The radius is dimension times Factor plus Offset, clamped to MinRadius and MaxRadius.
/// A zero or unavailable source dimension produces zero even when MinRadius is positive.
/// WPF may scale the resulting radii to fit the rendered geometry. Corners affects only
/// CornerRadius targets; double targets receive the scalar radius for shadows and similar uses.
/// Configure options before applying the extension; each binding captures its own options.
/// </para>
/// </remarks>
[MarkupExtensionReturnType(typeof(object))]
public sealed class PillCornerConverter : MarkupExtension
{
    /// <summary>
    /// Gets or sets the physical corners to round. The default is all four corners.
    /// </summary>
    public CornerRadiusCorners Corners { get; set; } = CornerRadiusCorners.All;

    /// <summary>
    /// Gets or sets the source dimension. The default is the shorter side.
    /// </summary>
    public PillCornerDimension Dimension { get; set; } = PillCornerDimension.ShorterSide;

    /// <summary>
    /// Gets or sets the finite, nonnegative radius multiplier. The default is 0.5.
    /// </summary>
    public double Factor { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets a finite offset in device-independent units, applied after scaling.
    /// The default is zero. Negative values can describe an inset outline.
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    /// Gets or sets the finite, nonnegative minimum radius. The default is zero.
    /// This limit applies only to selected corners of a source with positive dimensions.
    /// </summary>
    public double MinRadius { get; set; }

    /// <summary>
    /// Gets or sets the maximum radius. The default is positive infinity (unbounded).
    /// Zero forces square corners. The value must be at least MinRadius.
    /// </summary>
    public double MaxRadius { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// Gets or sets the name of the element supplying actual dimensions.
    /// The default is null. Specify either ElementName or RelativeSource, not both.
    /// </summary>
    public string? ElementName { get; set; }

    /// <summary>
    /// Gets or sets a relative source for actual dimensions, such as a templated parent.
    /// When both source options are null, the binding uses RelativeSource.Self.
    /// </summary>
    public RelativeSource? RelativeSource { get; set; }

    /// <summary>
    /// Creates an independent one-way binding for use with BindingOperations.SetBinding.
    /// </summary>
    /// <returns>A binding that tracks the selected source's actual width and height.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A numeric or enum option is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Both source options are specified.
    /// </exception>
    public MultiBinding CreateBinding()
    {
        ValidateOptions();
        var binding = new MultiBinding
        {
            Mode = BindingMode.OneWay,
            Converter = new RadiusConverter(Corners, Dimension, Factor, Offset, MinRadius, MaxRadius),
        };

        binding.Bindings.Add(CreateDimensionBinding(nameof(FrameworkElement.ActualWidth)));
        binding.Bindings.Add(CreateDimensionBinding(nameof(FrameworkElement.ActualHeight)));

        return binding;
    }

    /// <summary>
    /// Supplies the size binding to a dependency property, style setter, or template.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider for the target.</param>
    /// <returns>The binding expression, or a deferred binding for a style or template.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // BindingBase handles deferred targets and style setters. Observing the two actual-size
        // dependency properties also lets WPF manage invalidation and binding lifetimes.
        return CreateBinding().ProvideValue(serviceProvider);
    }

    /// <summary>
    /// Creates a dimension binding with the same source selection for both axes.
    /// </summary>
    private Binding CreateDimensionBinding(string path)
    {
        var binding = new Binding(path) { Mode = BindingMode.OneWay };
        if (ElementName is not null)
        {
            binding.ElementName = ElementName;
        }
        else
        {
            binding.RelativeSource = RelativeSource ?? System.Windows.Data.RelativeSource.Self;
        }

        return binding;
    }

    /// <summary>
    /// Rejects invalid configuration before WPF installs the binding.
    /// </summary>
    private void ValidateOptions()
    {
        if ((Corners & ~CornerRadiusCorners.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Corners));
        }

        if (!Enum.IsDefined(typeof(PillCornerDimension), Dimension))
        {
            throw new ArgumentOutOfRangeException(nameof(Dimension));
        }

        if (!IsFinite(Factor) || Factor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Factor));
        }

        if (!IsFinite(Offset))
        {
            throw new ArgumentOutOfRangeException(nameof(Offset));
        }

        if (!IsFinite(MinRadius) || MinRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinRadius));
        }

        if (double.IsNaN(MaxRadius) || MaxRadius < MinRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRadius));
        }

        if (ElementName is not null && string.IsNullOrWhiteSpace(ElementName))
        {
            throw new ArgumentException("An element name must not be empty.", nameof(ElementName));
        }

        if (ElementName is not null && RelativeSource is not null)
        {
            throw new InvalidOperationException("Specify either ElementName or RelativeSource, not both.");
        }
    }

    /// <summary>
    /// Checks that a value can participate in finite radius arithmetic on all target frameworks.
    /// </summary>
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Converts observed dimensions using a snapshot of the extension's configuration.
    /// </summary>
    private sealed class RadiusConverter(
        CornerRadiusCorners corners,
        PillCornerDimension dimension,
        double factor,
        double offset,
        double minimum,
        double maximum) : IMultiValueConverter
    {
        /// <inheritdoc />
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(CornerRadius) && targetType != typeof(double))
            {
                return DependencyProperty.UnsetValue;
            }

            var radius = 0D;

            if (values.Length == 2
                && values[0] is double width && IsFinite(width) && width > 0
                && values[1] is double height && IsFinite(height) && height > 0)
            {
                var length = dimension switch
                {
                    PillCornerDimension.Width  => width,
                    PillCornerDimension.Height => height,
                    _ => Math.Min(width, height),
                };

                // Saturate overflow so every successful conversion supplies a finite WPF radius.
                var scaled = Math.Min(length * factor, double.MaxValue);
                radius = Math.Max(minimum, Math.Min(scaled + offset, Math.Min(maximum, double.MaxValue)));
            }

            if (targetType == typeof(double))
            {
                return radius;
            }

            return new CornerRadius(
                (corners & CornerRadiusCorners.TopLeft)     != 0 ? radius : 0,
                (corners & CornerRadiusCorners.TopRight)    != 0 ? radius : 0,
                (corners & CornerRadiusCorners.BottomRight) != 0 ? radius : 0,
                (corners & CornerRadiusCorners.BottomLeft)  != 0 ? radius : 0
            );
        }

        /// <inheritdoc />
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Pill corner bindings are one-way.");
        }
    }
}
