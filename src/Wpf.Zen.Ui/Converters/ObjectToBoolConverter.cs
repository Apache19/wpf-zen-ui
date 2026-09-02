// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts an object to a Boolean value by comparing it with configurable true and false values.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TrueValue"/> and <see cref="FalseValue"/> are optional. A value equal to
/// <see cref="TrueValue"/> converts to <see langword="true"/>, and a value equal to
/// <see cref="FalseValue"/> converts to <see langword="false"/>. When neither configured value
/// matches, <see cref="UnmatchedValue"/> is returned; its default is <see langword="false"/>.
/// Consequently, an unconfigured converter always returns <see langword="false"/>, while setting
/// only <see cref="TrueValue"/> provides the common "equal to this value" predicate.
/// </para>
/// <para>
/// The default comparison uses <see cref="object.Equals(object?, object?)"/> and therefore does not
/// coerce values between types. For example, the integer <c>1</c> is not equal to the string
/// <c>"1"</c>. Set <see cref="Comparer"/> when the application needs different equality semantics,
/// such as case-insensitive string comparison.
/// </para>
/// <para>
/// The default value of both mapping properties is <see cref="DependencyProperty.UnsetValue"/>.
/// This sentinel distinguishes an omitted mapping from a mapping explicitly set to <see langword="null"/>,
/// allowing <see langword="null"/> to be matched and restored during reverse conversion.
/// </para>
/// <para>
/// If both configured values match the same input, the mapping is ambiguous and
/// <see cref="Convert(object?, Type, object?, CultureInfo)"/> returns
/// <see cref="DependencyProperty.UnsetValue"/> so that WPF can apply the binding fallback or target
/// property default. Unavailable binding-engine values are handled in the same way.
/// </para>
/// <para>
/// Reverse conversion is supported for configured mappings. A Boolean value maps back to its
/// corresponding object. If that mapping is not configured, or the two configured mappings are
/// equal, reverse conversion returns <see cref="Binding.DoNothing"/> so a TwoWay binding does not
/// overwrite its source with a value that the converter cannot reconstruct unambiguously.
/// </para>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- True only when State equals Ready; every other value is false. --&gt;
/// &lt;CheckBox IsChecked="{Binding State,
///     Converter={converters:ObjectToBoolConverter TrueValue={x:Static local:State.Ready}}}" /&gt;
///
/// &lt;!-- A reversible mapping suitable for TwoWay bindings. --&gt;
/// &lt;converters:ObjectToBoolConverter x:Key="EnabledStateConverter"
///     TrueValue="Enabled" FalseValue="Disabled" /&gt;
/// &lt;CheckBox IsChecked="{Binding State, Mode=TwoWay,
///     Converter={StaticResource EnabledStateConverter}}" /&gt;
///
/// &lt;!-- False only for the configured value; unmatched values become true. --&gt;
/// &lt;converters:ObjectToBoolConverter x:Key="NotDisabledConverter"
///     FalseValue="Disabled" UnmatchedValue="True" /&gt;
/// </code>
/// </remarks>
#pragma warning disable WPF0072 // The source accepts any object by design.
[ValueConversion(typeof(object), typeof(bool))]
#pragma warning restore WPF0072
[MarkupExtensionReturnType(typeof(ObjectToBoolConverter))]
public sealed class ObjectToBoolConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the object that converts to <see langword="true"/>.
    /// </summary>
    /// <value>
    /// The object representing <see langword="true"/>, including <see langword="null"/> when set
    /// explicitly. The default is <see cref="DependencyProperty.UnsetValue"/>, which means that no
    /// true mapping is configured.
    /// </value>
    public object? TrueValue { get; set; } = DependencyProperty.UnsetValue;

    /// <summary>
    /// Gets or sets the object that converts to <see langword="false"/>.
    /// </summary>
    /// <value>
    /// The object representing <see langword="false"/>, including <see langword="null"/> when set
    /// explicitly. The default is <see cref="DependencyProperty.UnsetValue"/>, which means that no
    /// false mapping is configured.
    /// </value>
    /// <remarks>
    /// A false mapping is most useful for reverse conversion or together with
    /// <see cref="UnmatchedValue"/> set to <see langword="true"/>.
    /// </remarks>
    public object? FalseValue { get; set; } = DependencyProperty.UnsetValue;

    /// <summary>
    /// Gets or sets a value indicating whether an input that matches neither configured mapping
    /// converts to <see langword="true"/>.
    /// </summary>
    /// <value><see langword="false"/> by default.</value>
    public bool UnmatchedValue { get; set; }

    /// <summary>
    /// Gets or sets the comparer used to match the input against the configured values.
    /// </summary>
    /// <value>
    /// An equality comparer, or <see langword="null"/> (the default) to use
    /// <see cref="object.Equals(object?, object?)"/>.
    /// </value>
    /// <remarks>
    /// The same comparer is used for both mappings. It should provide stable, symmetric equality
    /// semantics; otherwise the converter cannot reliably detect ambiguous mappings.
    /// </remarks>
    public IEqualityComparer? Comparer { get; set; }

    /// <summary>
    /// Compares an input object with the configured true and false values.
    /// </summary>
    /// <param name="value">The value produced by the source binding.</param>
    /// <param name="targetType">The target property type. The returned value is Boolean.</param>
    /// <param name="parameter">An optional converter parameter; this converter does not use it.</param>
    /// <param name="culture">
    /// The binding culture. The converter does not use it directly; a configured comparer controls
    /// any culture-sensitive equality semantics.
    /// </param>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/> according to the configured mapping;
    /// otherwise <see cref="DependencyProperty.UnsetValue"/> when the input is unavailable or the
    /// mapping is ambiguous.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // These objects are control signals owned by the binding engine, not application data.
        // Preserving the failure state lets FallbackValue and the target default behave normally.
        if (IsBindingSentinel(value))
        {
            return DependencyProperty.UnsetValue;
        }

        bool matchesTrue = IsConfigured(TrueValue) && AreEqual(value, TrueValue);
        bool matchesFalse = IsConfigured(FalseValue) && AreEqual(value, FalseValue);

        // A two-way mapping must remain deterministic. Silently preferring either side here would
        // make ConvertBack unable to round-trip one of the Boolean values.
        if (matchesTrue && matchesFalse)
        {
            return DependencyProperty.UnsetValue;
        }

        if (matchesTrue)
        {
            return true;
        }

        return matchesFalse ? false : UnmatchedValue;
    }

    /// <summary>
    /// Converts a Boolean value back to its configured object representation.
    /// </summary>
    /// <param name="value">The Boolean value produced by the target property.</param>
    /// <param name="targetType">The source property type.</param>
    /// <param name="parameter">An optional converter parameter; this converter does not use it.</param>
    /// <param name="culture">The binding culture; this converter performs no type coercion.</param>
    /// <returns>
    /// The configured <see cref="TrueValue"/> or <see cref="FalseValue"/>. Returns
    /// <see cref="Binding.DoNothing"/> when the requested mapping is omitted or the mappings are
    /// ambiguous, or <see cref="DependencyProperty.UnsetValue"/> when <paramref name="value"/> is
    /// not Boolean.
    /// </returns>
    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (value is not bool booleanValue)
        {
            return DependencyProperty.UnsetValue;
        }

        // Equal mappings cannot encode both Boolean states. Avoid changing the source because the
        // next forward conversion would be ambiguous and could not reproduce the target value.
        if (
            IsConfigured(TrueValue)
            && IsConfigured(FalseValue)
            && AreEqual(TrueValue, FalseValue)
        )
        {
            return Binding.DoNothing;
        }

        object? mappedValue = booleanValue ? TrueValue : FalseValue;

        // DoNothing is intentional here: absence of a reverse mapping is not a validation failure,
        // and the source must retain its current value rather than receive the forward fallback.
        return IsConfigured(mappedValue) ? mappedValue : Binding.DoNothing;
    }

    /// <summary>
    /// Returns this converter instance for direct use from XAML markup.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider supplied by the markup system.</param>
    /// <returns>The current converter instance.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private static bool IsConfigured(object? value)
    {
        return !ReferenceEquals(value, DependencyProperty.UnsetValue);
    }

    private static bool IsBindingSentinel(object? value)
    {
        return ReferenceEquals(value, DependencyProperty.UnsetValue)
            || ReferenceEquals(value, Binding.DoNothing);
    }

    private bool AreEqual(object? left, object? right)
    {
        return Comparer?.Equals(left, right) ?? Equals(left, right);
    }
}
