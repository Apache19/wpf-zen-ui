// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides attached properties that control <see cref="ScrollViewer"/> layout behavior.
/// </summary>
public static class ScrollViewerAssist
{
    /// <summary>
    /// Identifies the <see cref="OverlayModeProperty"/> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty OverlayModeProperty = DependencyProperty.RegisterAttached(
        "OverlayMode",
        typeof(ScrollViewerOverlayMode),
        typeof(ScrollViewerAssist),
        new FrameworkPropertyMetadata(ScrollViewerOverlayMode.Overlay)
    );

    /// <summary>
    /// Helper for getting <see cref="OverlayModeProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="OverlayModeProperty"/> from.
    /// </param>
    /// <returns>OverlayMode property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
    public static ScrollViewerOverlayMode GetOverlayMode(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (ScrollViewerOverlayMode)element.GetValue(OverlayModeProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="OverlayModeProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="ScrollViewer"/> to set <see cref="OverlayModeProperty"/> on.
    /// </param>
    /// <param name="value">OverlayMode property value.</param>
    public static void SetOverlayMode(DependencyObject element, ScrollViewerOverlayMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(OverlayModeProperty, value);
    }
}
