// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides an inheritable <see cref="Orientation"/> attached property for WPF controls.
/// </summary>
public static class OrientationAssist
{
    /// <summary>
    /// Gets or sets the <see cref="Orientation"/> for layout.
    /// This is an inheritable attached property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.RegisterAttached(
            "Orientation",
            typeof(Orientation),
            typeof(OrientationAssist),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Helper for getting <see cref="OrientationProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="OrientationProperty"/> from.
    /// </param>
    /// <returns>Orientation property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static Orientation GetOrientation(DependencyObject element)
    {
        return (Orientation)element.GetValue(OrientationProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="OrientationProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="OrientationProperty"/> on.
    /// </param>
    /// <param name="value">Orientation property value.</param>
    public static void SetOrientation(DependencyObject element, Orientation value)
    {
        element.SetValue(OrientationProperty, value);
    }
}