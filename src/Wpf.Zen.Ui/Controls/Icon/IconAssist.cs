// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines the template contract for a control's primary icon slot.
/// </summary>
/// <remarks>
/// <para>
/// The attached properties deliberately live outside any concrete control type. Controls with
/// unrelated base classes can therefore expose the same icon API, and native WPF controls can opt
/// into the contract from a custom template without requiring a derived control solely for an icon.
/// </para>
/// <para>
/// Setting these properties does not create visual content by itself. A control template must bind
/// its icon host to <see cref="IconProperty"/> and <see cref="IconMarginProperty"/>. This separation
/// keeps the public slot contract independent from the template element used to present it.
/// </para>
/// </remarks>
public static class IconAssist
{
    #region Attached-DP: Icon

    /// <summary>
    /// Identifies the attached property that stores arbitrary WPF content for the primary icon slot.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="null"/>. The value is not inherited because an icon
    /// belongs to one control; inheriting it into nested controls would accidentally duplicate the
    /// same slot content and can attempt to parent one visual more than once.
    /// </remarks>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon",
        typeof(object),
        typeof(IconAssist),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Helper for getting <see cref="IconProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IconProperty"/> from.
    /// </param>
    /// <returns>Icon property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(Control))]
    public static object? GetIcon(DependencyObject element)
    {
        return element.GetValue(IconProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="IconProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IconProperty"/> on.
    /// </param>
    /// <param name="value">Icon property value.</param>
    public static void SetIcon(DependencyObject element, object? value)
    {
        element.SetValue(IconProperty, value);
    }

    #endregion

    #region Attached-DP: IconMargin

    /// <summary>
    /// Identifies the attached property that controls the external margin of the primary icon slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default value is an empty <see cref="Thickness"/>. Individual control styles should set
    /// their own visual default because appropriate spacing depends on the control and icon
    /// placement. A local value supplied by an application then naturally takes precedence over the
    /// style default.
    /// </para>
    /// <para>
    /// This property is intentionally not inheritable. Slot layout is local to the templated control
    /// and must not leak into controls nested inside its content. The template should apply the value
    /// to the outer element that owns the complete icon slot, which is not necessarily the
    /// <see cref="IconPresenter"/> itself.
    /// </para>
    /// </remarks>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.RegisterAttached(
        "IconMargin",
        typeof(Thickness),
        typeof(IconAssist),
        new FrameworkPropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets the external margin used by the primary icon slot of <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The control whose icon-slot margin is read.</param>
    /// <returns>The effective icon-slot margin.</returns>
    [AttachedPropertyBrowsableForType(typeof(Control))]
    public static Thickness GetIconMargin(DependencyObject element)
    {
        return (Thickness)element.GetValue(IconMarginProperty);
    }

    /// <summary>
    /// Sets the external margin used by the primary icon slot of <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The control whose icon-slot margin is changed.</param>
    /// <param name="value">The new external margin for the icon slot.</param>
    public static void SetIconMargin(DependencyObject element, Thickness value)
    {
        element.SetValue(IconMarginProperty, value);
    }

    #endregion
}
