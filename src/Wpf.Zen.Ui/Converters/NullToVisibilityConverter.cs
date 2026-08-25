// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts a reference value, string, or collection to a <see cref="Visibility"/> value
/// based on whether the input has a value.
/// </summary>
/// <remarks>
/// <para>
/// The converter uses two configurable <see cref="Visibility"/> properties to define
/// the values corresponding to null and non-null inputs.
/// </para>
/// <para>
/// The default mapping is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>null</c> → <see cref="NullValue"/> (default: <see cref="Visibility.Collapsed"/>)
/// </description>
/// </item>
/// <item>
/// <description>
/// non-null → <see cref="NotNullValue"/> (default: <see cref="Visibility.Visible"/>)
/// </description>
/// </item>
/// </list>
/// <para>
/// When <see cref="TreatEmptyStringAsNull"/> is <c>true</c>, empty strings
/// (<see cref="string.Empty"/> or <c>""</c>) are also treated as null and
/// mapped to <see cref="NullValue"/>.
/// </para>
/// <para>
/// When <see cref="TreatEmptyCollectionAsNull"/> is <c>true</c>, an empty
/// <see cref="IEnumerable"/> is treated as null. This option is enabled by default.
/// </para>
/// <para>
/// A value converter is not re-evaluated merely because the collection supplied as its value
/// raises <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/>.
/// For dynamic visibility, bind to the collection's <c>Count</c> property and enable
/// <see cref="TreatZeroAsNull"/>. <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
/// raises a property-change notification for <c>Count</c> after every size-changing mutation,
/// so the WPF binding engine re-evaluates the converter without a converter-owned subscription.
/// </para>
/// <para>
/// Any null-to-Visibility mapping can be configured by setting
/// <see cref="NullValue"/> and <see cref="NotNullValue"/>.
/// </para>
/// <para>
/// Reverse conversion is not supported. Because <see cref="Visibility"/> cannot
/// encode the original non-null object, <see cref="ConvertBack"/> always returns
/// <see cref="DependencyProperty.UnsetValue"/>.
/// </para>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- Default: null→Collapsed, non-null→Visible --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToVisibility" /&gt;
///
/// &lt;!-- Inverted: null→Visible, non-null→Collapsed --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToVisibilityInvert"
///     NullValue="Visible" NotNullValue="Collapsed" /&gt;
///
/// &lt;!-- Hidden mode: null→Hidden, non-null→Visible (preserves layout space) --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToHidden"
///     NullValue="Hidden" /&gt;
///
/// &lt;!-- Treat empty strings as null --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullOrEmptyToVisibility"
///     TreatEmptyStringAsNull="True" /&gt;
///
/// &lt;!-- Dynamic collection visibility; ObservableCollection.Count notifies on add/remove --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="CollectionCountToVisibility"
///     TreatZeroAsNull="True" /&gt;
/// &lt;TextBlock Visibility="{Binding Items.Count, Converter={StaticResource CollectionCountToVisibility}}" /&gt;
/// </code>
/// </remarks>
#pragma warning disable WPF0072 // ValueConversion must use correct types
[ValueConversion(typeof(object), typeof(Visibility))]
#pragma warning restore WPF0072 // ValueConversion must use correct types
[MarkupExtensionReturnType(typeof(NullToVisibilityConverter))]
public sealed class NullToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the <see cref="Visibility"/> value returned when the input is <c>null</c>.
    /// The default value is <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="TreatEmptyStringAsNull"/> is <c>true</c>, this value is also
    /// returned for empty strings.
    /// </remarks>
    public Visibility NullValue { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Gets or sets the <see cref="Visibility"/> value returned when the input is not <c>null</c>.
    /// The default value is <see cref="Visibility.Visible"/>.
    /// </summary>
    public Visibility NotNullValue { get; set; } = Visibility.Visible;

    /// <summary>
    /// Gets or sets a value indicating whether empty strings should be treated as <c>null</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to treat <see cref="string.Empty"/> (or <c>""</c>) as null;
    /// <c>false</c> (default) to treat empty strings as non-null values.
    /// </value>
    /// <remarks>
    /// When enabled, the input is considered null if it is either <c>null</c> (no object)
    /// or a <see cref="string"/> with zero length.
    /// </remarks>
    public bool TreatEmptyStringAsNull { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an empty collection should be treated as <c>null</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> (default) to map an empty <see cref="IEnumerable"/> to <see cref="NullValue"/>;
    /// <c>false</c> to treat every non-null collection as a value regardless of its item count.
    /// </value>
    /// <remarks>
    /// Strings are excluded from collection handling and remain governed by
    /// <see cref="TreatEmptyStringAsNull"/>. Collections without a directly available count are
    /// inspected only until their first item, so determining emptiness does not enumerate the
    /// complete sequence.
    /// </remarks>
    public bool TreatEmptyCollectionAsNull { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the integer value zero should be treated as <c>null</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to map zero to <see cref="NullValue"/>; <c>false</c> (default) to preserve the
    /// converter's normal rule that every non-null integer maps to <see cref="NotNullValue"/>.
    /// </value>
    /// <remarks>
    /// This option is intended for bindings to a collection's <c>Count</c> property. In particular,
    /// binding to <c>ObservableCollection.Count</c> provides dynamic updates because WPF observes
    /// the collection's count property notifications. It is opt-in so existing bindings of ordinary
    /// numeric values retain their null-only semantics.
    /// </remarks>
    public bool TreatZeroAsNull { get; set; } = true;

    /// <summary>
    /// Converts a value to a <see cref="Visibility"/> based on whether it is null or configured as empty.
    /// </summary>
    /// <param name="value">The input value to check for null.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">An optional converter parameter (not used).</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>
    /// <see cref="NullValue"/> if the input is null or matches one of the enabled empty-value rules;
    /// otherwise, <see cref="NotNullValue"/>.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null;

        if (!isNull && TreatEmptyStringAsNull && value is string s)
        {
            isNull = s.Length == 0;
        }

        if (!isNull && TreatEmptyCollectionAsNull && value is IEnumerable enumerable and not string)
        {
            isNull = IsEmpty(enumerable);
        }

        if (!isNull && TreatZeroAsNull && value is int count)
        {
            isNull = count == 0;
        }

        return isNull ? NullValue : NotNullValue;
    }

    /// <summary>
    /// Determines whether an enumerable exposes at least one item without walking the full sequence.
    /// </summary>
    /// <remarks>
    /// <see cref="ICollection.Count"/> avoids allocating an enumerator for the common WPF collection
    /// case. The fallback deliberately calls <see cref="IEnumerator.MoveNext"/> only once so lazy
    /// sequences are not materialized merely to decide visibility.
    /// </remarks>
    private static bool IsEmpty(IEnumerable enumerable)
    {
        if (enumerable is ICollection collection)
        {
            return collection.Count == 0;
        }

        IEnumerator enumerator = enumerable.GetEnumerator();

        try
        {
            return !enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Reverse conversion is not supported. Always returns <see cref="DependencyProperty.UnsetValue"/>.
    /// </summary>
    /// <returns><see cref="DependencyProperty.UnsetValue"/> in all cases.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Provides the converter instance to the XAML markup extension system,
    /// enabling direct use without explicit resource key declaration.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
