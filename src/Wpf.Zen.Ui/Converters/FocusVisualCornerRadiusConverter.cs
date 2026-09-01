// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Selects the corner radius used by the default keyboard-focus visual from the control being adorned.
/// </summary>
/// <remarks>
/// <para>
/// The converter expects the adorned <see cref="DependencyObject" />, its conventional <c>CornerRadius</c>
/// property value, and its <see cref="Border.CornerRadiusProperty" /> value, in that order. A
/// <see cref="MultiBinding" /> supplies all three values so changes to either radius source update an existing
/// focus visual.
/// </para>
/// <para>
/// WPF attached dependency properties return their metadata default even when they have never been configured.
/// Consequently, an unset <see cref="Border.CornerRadiusProperty" /> produces a valid zero radius and cannot be
/// distinguished with <see cref="BindingBase.FallbackValue" /> alone. This converter inspects the dependency-property
/// value source so an untouched metadata default falls back to <see cref="FallbackValue" />, while a deliberately
/// configured zero radius remains square.
/// </para>
/// </remarks>
[MarkupExtensionReturnType(typeof(FocusVisualCornerRadiusConverter))]
public sealed class FocusVisualCornerRadiusConverter : MarkupExtension, IMultiValueConverter
{
    private const int AdornedElementIndex = 0;
    private const int ConventionalCornerRadiusIndex = 1;
    private const int AttachedCornerRadiusIndex = 2;

    private static readonly object CornerRadiusPropertyCacheLock = new();
    private static readonly Dictionary<Type, DependencyProperty?> CornerRadiusPropertyCache = new();

    /// <summary>
    /// Gets or sets the radius returned when the adorned control exposes no configured corner radius.
    /// The default is <c>4,4,4,4</c>.
    /// </summary>
    public CornerRadius FallbackValue { get; set; } = new(4);

    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length <= AdornedElementIndex || values[AdornedElementIndex] is not DependencyObject adornedElement)
        {
            return FallbackValue;
        }

        var conventionalProperty = GetConventionalCornerRadiusProperty(adornedElement.GetType());
        if (
            values.Length > ConventionalCornerRadiusIndex
            && TryGetConfiguredCornerRadius(
                adornedElement,
                conventionalProperty,
                values[ConventionalCornerRadiusIndex],
                out var cornerRadius
            )
        )
        {
            return cornerRadius;
        }

        if (
            values.Length > AttachedCornerRadiusIndex
            && TryGetConfiguredCornerRadius(
                adornedElement,
                Border.CornerRadiusProperty,
                values[AttachedCornerRadiusIndex],
                out cornerRadius
            )
        )
        {
            return cornerRadius;
        }

        return FallbackValue;
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private static bool TryGetConfiguredCornerRadius(
        DependencyObject adornedElement,
        DependencyProperty? property,
        object value,
        out CornerRadius cornerRadius
    )
    {
        if (value is not CornerRadius candidate)
        {
            cornerRadius = default;
            return false;
        }

        // A non-zero effective value already carries useful geometry, including the uncommon case where a control
        // declares a non-zero metadata default. Only zero is ambiguous and requires inspecting its value source.
        if (candidate != default || property is null)
        {
            cornerRadius = candidate;
            return true;
        }

        var valueSource = DependencyPropertyHelper.GetValueSource(adornedElement, property);
        if (valueSource.BaseValueSource != BaseValueSource.Default)
        {
            // Zero from a local value, style, trigger, binding, or inheritance is deliberate and must retain the
            // square-corner contract. Only the dependency property's untouched metadata default means "unspecified".
            cornerRadius = candidate;
            return true;
        }

        cornerRadius = default;
        return false;
    }

    private static DependencyProperty? GetConventionalCornerRadiusProperty(Type adornedElementType)
    {
        lock (CornerRadiusPropertyCacheLock)
        {
            if (CornerRadiusPropertyCache.TryGetValue(adornedElementType, out var property))
            {
                return property;
            }

            // DependencyPropertyDescriptor resolves a public CornerRadius dependency property registered by the
            // control or one of its base types. Controls without that contract simply use the attached-property or
            // fallback paths; no reflection or control-specific registration is required.
            property = DependencyPropertyDescriptor.FromName(
                nameof(Border.CornerRadius),
                adornedElementType,
                adornedElementType
            )?.DependencyProperty;

            CornerRadiusPropertyCache.Add(adornedElementType, property);
            return property;
        }
    }
}
