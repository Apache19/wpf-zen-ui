// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides per-element customization for the visual drawn when keyboard focus enters an element.
/// </summary>
/// <remarks>
/// <para>
/// WPF resolves its implicit keyboard-focus style through
/// <see cref="SystemParameters.FocusVisualStyleKey"/> only while
/// <see cref="FrameworkElement.FocusVisualStyle"/> retains its framework default value. Replacing that resource
/// key inside a control style changes where the lookup terminates and prevents application or ancestor resources
/// from participating in WPF's normal lookup chain.
/// </para>
/// <para>
/// <see cref="PresenterStyleProperty"/> customizes only the presenter hosted by WPF Zen UI's global focus style.
/// The framework-level focus style therefore remains implicit, while individual controls can change the presenter's
/// margin, template, geometry, or any other <see cref="Control"/> property. A custom presenter style must provide a
/// complete visual; use <see cref="Theming.StyleKeys.DefaultFocusVisualPresenterStyle"/> as its
/// <see cref="Style.BasedOn"/> value when only ordinary presenter properties need to change.
/// </para>
/// </remarks>
[StyleTypedProperty(Property = "PresenterStyle", StyleTargetType = typeof(FocusVisualPresenter))]
public static class FocusVisual
{
    /// <summary>
    /// Identifies the <c>PresenterStyle</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="null"/>, which selects
    /// <see cref="Theming.StyleKeys.DefaultFocusVisualPresenterStyle"/>. The property does not inherit: focus geometry
    /// is a contract of the element being adorned and must not leak into descendants with a different outline.
    /// </remarks>
    public static readonly DependencyProperty PresenterStyleProperty = DependencyProperty.RegisterAttached(
        "PresenterStyle",
        typeof(Style),
        typeof(FocusVisual),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets the style used by the inner focus-visual presenter for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose keyboard-focus visual is being configured.</param>
    /// <returns>
    /// The custom presenter style, or <see langword="null"/> when the global default presenter should be used.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static Style? GetPresenterStyle(DependencyObject element)
    {
        return (Style?)element.GetValue(PresenterStyleProperty);
    }

    /// <summary>
    /// Sets the style used by the inner focus-visual presenter for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose keyboard-focus visual is being configured.</param>
    /// <param name="value">
    /// A style targeting <see cref="FocusVisualPresenter"/>, or <see langword="null"/> to restore the global default
    /// presenter. The style may replace <see cref="Control.Template"/> to draw non-rectangular or otherwise
    /// specialized outlines.
    /// </param>
    public static void SetPresenterStyle(DependencyObject element, Style? value)
    {
        element.SetValue(PresenterStyleProperty, value);
    }

    /// <summary>
    /// Identifies the <c>CornerRadius</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="null"/>, which asks <see cref="FocusVisualPresenter"/> to inspect the
    /// adorned element's conventional <c>CornerRadius</c> dependency property and then its explicitly configured
    /// <see cref="Border.CornerRadiusProperty"/> value. A non-null value, including zero, is always authoritative.
    /// The property does not affect layout or rendering on the adorned element itself.
    /// </remarks>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius",
        typeof(CornerRadius?),
        typeof(FocusVisual),
        new FrameworkPropertyMetadata(null),
        ValidateCornerRadius
    );

    /// <summary>
    /// Gets the explicit focus-outline corner radius configured for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose keyboard-focus geometry is being queried.</param>
    /// <returns>The explicit radius, or <see langword="null"/> to use automatic outline resolution.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static CornerRadius? GetCornerRadius(DependencyObject element)
    {
        return (CornerRadius?)element.GetValue(CornerRadiusProperty);
    }

    /// <summary>
    /// Sets the explicit focus-outline corner radius for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose keyboard-focus geometry is being configured.</param>
    /// <param name="value">
    /// The radius to use, including zero for a square outline, or <see langword="null"/> to restore automatic
    /// outline resolution.
    /// </param>
    public static void SetCornerRadius(DependencyObject element, CornerRadius? value)
    {
        element.SetValue(CornerRadiusProperty, value);
    }

    private static bool ValidateCornerRadius(object? value)
    {
        return value is null
            || (value is CornerRadius cornerRadius && Border.CornerRadiusProperty.IsValidValue(cornerRadius));
    }
}
