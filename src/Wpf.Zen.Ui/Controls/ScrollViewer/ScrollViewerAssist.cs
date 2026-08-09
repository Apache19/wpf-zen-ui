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
    /// Identifies the <see cref="GetHideButtons"/> and <see cref="SetHideButtons"/> attached
    /// dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. When set on a <see cref="ScrollViewer"/>,
    /// the line-decrement and line-increment buttons in both scroll bars are collapsed while the
    /// track and thumb remain available. The property may also be set on a control whose template
    /// creates a <see cref="PassiveScrollViewer"/>; that viewer forwards the value from its
    /// templated parent in the same way that WPF forwards its standard scrolling attached
    /// properties.
    /// </remarks>
    public static readonly DependencyProperty HideButtonsProperty = DependencyProperty.RegisterAttached(
        "HideButtons",
        typeof(bool),
        typeof(ScrollViewerAssist),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>
    /// Gets whether the line-decrement and line-increment buttons in an element's scroll bars are
    /// hidden.
    /// </summary>
    /// <param name="element">The scroll viewer or template-owning control to query.</param>
    /// <returns>
    /// <see langword="true"/> when the scroll-bar buttons are hidden; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool GetHideButtons(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(HideButtonsProperty);
    }

    /// <summary>
    /// Sets whether the line-decrement and line-increment buttons in an element's scroll bars are
    /// hidden.
    /// </summary>
    /// <param name="element">The scroll viewer or template-owning control to configure.</param>
    /// <param name="value">
    /// <see langword="true"/> to collapse the buttons and give their space to the track;
    /// otherwise, <see langword="false"/>.
    /// </param>
    public static void SetHideButtons(DependencyObject element, bool value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(HideButtonsProperty, value);
    }

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
