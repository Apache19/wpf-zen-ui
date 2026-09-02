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
[StyleTypedProperty(Property = "PresenterStyle", StyleTargetType = typeof(Control))]
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
    /// A style targeting <see cref="Control"/>, or <see langword="null"/> to restore the global default presenter.
    /// The style may replace <see cref="Control.Template"/> to draw non-rectangular or otherwise specialized outlines.
    /// </param>
    public static void SetPresenterStyle(DependencyObject element, Style? value)
    {
        element.SetValue(PresenterStyleProperty, value);
    }
}
