// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides attached properties for assigning icons to native WPF controls.
/// </summary>
public static class IconAssist
{
    /// <summary>
    /// Identifies the attached property that stores arbitrary WPF content for an icon slot.
    /// </summary>
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
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
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
}
