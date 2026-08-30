// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Compares two numeric values supplied by a <see cref="MultiBinding"/> and returns a
/// <see cref="bool"/> result.
/// </summary>
/// <remarks>
/// <para>
/// The first two binding values are converted to <see cref="double"/> by using the culture
/// supplied by the binding engine. The <c>parameter</c> passed to
/// <see cref="Convert(object[], Type, object?, CultureInfo)"/> selects one of the supported
/// operators: <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>==</c>, or <c>!=</c>.
/// When no parameter is supplied, <c>&gt;</c> is used.
/// </para>
/// <para>
/// Set <see cref="Tolerance"/> when comparisons need an absolute floating-point tolerance.
/// Values whose absolute difference is within that tolerance compare as equal, so all six
/// operators remain mutually consistent. The default tolerance is <c>0</c>, which performs
/// exact comparisons.
/// </para>
/// <para>
/// Invalid or unavailable binding values produce <see cref="DependencyProperty.UnsetValue"/>,
/// allowing WPF to use the multi-binding's fallback or the target property's default value.
/// <see cref="double.NaN"/> follows normal floating-point semantics: it is unequal to every
/// value, including itself, and is neither less than nor greater than another value.
/// </para>
/// <para>Usage example:</para>
/// <code>
/// &lt;MultiBinding Converter="{converters:ValueComparisonConverter}" ConverterParameter="&gt;="&gt;
///   &lt;Binding Path="CurrentValue"/&gt;
///   &lt;Binding Path="MinimumValue"/&gt;
/// &lt;/MultiBinding&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(object[]), typeof(bool))]
[MarkupExtensionReturnType(typeof(ValueComparisonConverter))]
public sealed class ValueComparisonConverter : MarkupExtension, IMultiValueConverter
{
    private double _tolerance;

    /// <summary>
    /// Gets or sets the non-negative absolute difference at which two finite values are
    /// considered equal. The default is <c>0</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative, <see cref="double.NaN"/>, or infinite.
    /// </exception>
    public double Tolerance
    {
        get => _tolerance;
        set
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Tolerance must be a finite, non-negative number."
                );
            }

            _tolerance = value;
        }
    }

    /// <summary>
    /// Converts the first two values to numbers and compares them with the operator supplied
    /// in <paramref name="parameter"/>.
    /// </summary>
    /// <param name="values">Exactly two values to compare, with the left operand first.</param>
    /// <param name="targetType">The type of the binding target. The converter returns a Boolean value.</param>
    /// <param name="parameter">
    /// One of <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>==</c>, or <c>!=</c>.
    /// A <see langword="null"/> parameter selects <c>&gt;</c>.
    /// </param>
    /// <param name="culture">The culture used when an operand requires numeric conversion.</param>
    /// <returns>
    /// The comparison result, or <see cref="DependencyProperty.UnsetValue"/> when the operands
    /// or operator cannot be converted.
    /// </returns>
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // WPF can invoke a MultiBinding converter while one of its child bindings still has no
        // value. Returning UnsetValue preserves the binding engine's fallback/default behavior.
        if (values is not { Length: 2 }
            || IsUnavailable(values[0])
            || IsUnavailable(values[1])
            || !TryConvertToDouble(values[0], culture, out double left)
            || !TryConvertToDouble(values[1], culture, out double right)
            || !TryGetOperator(parameter, out string comparisonOperator))
        {
            return DependencyProperty.UnsetValue;
        }

        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return comparisonOperator == "!=";
        }

        int comparison = Compare(left, right);

        return comparisonOperator switch
        {
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            "==" => comparison == 0,
            "!=" => comparison != 0,
            _ => DependencyProperty.UnsetValue,
        };
    }

    /// <summary>
    /// Does not perform reverse conversion because a comparison result cannot uniquely
    /// reconstruct either operand.
    /// </summary>
    /// <returns>
    /// An empty array, which instructs WPF not to update any of the source bindings.
    /// </returns>
    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object? parameter,
        CultureInfo culture
    )
    {
        return [];
    }

    /// <summary>
    /// Returns this converter instance for direct use from XAML markup.
    /// </summary>
    /// <returns>The current converter instance.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private static bool IsUnavailable(object? value)
    {
        return value is null
            || ReferenceEquals(value, DependencyProperty.UnsetValue)
            || ReferenceEquals(value, Binding.DoNothing);
    }

    private static bool TryConvertToDouble(object value, CultureInfo culture, out double result)
    {
        try
        {
            result = System.Convert.ToDouble(value, culture);
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or OverflowException
        )
        {
            // These exceptions describe expected binding-data failures. Other exceptions from a
            // custom IConvertible implementation are programming errors and must remain visible.
            result = default;
            return false;
        }
    }

    private static bool TryGetOperator(object? parameter, out string comparisonOperator)
    {
        comparisonOperator = parameter switch
        {
            null => ">",
            string text => text.Trim(),
            _ => string.Empty,
        };

        return comparisonOperator is ">" or ">=" or "<" or "<=" or "==" or "!=";
    }

    private int Compare(double left, double right)
    {
        // Test exact equality first so equal infinities compare correctly; subtracting them would
        // produce NaN. NaN operands have already been handled by the caller.
        if (left == right)
        {
            return 0;
        }

        if (
            !double.IsInfinity(left)
            && !double.IsInfinity(right)
            && Math.Abs(left - right) <= Tolerance
        )
        {
            return 0;
        }

        return left < right ? -1 : 1;
    }
}
