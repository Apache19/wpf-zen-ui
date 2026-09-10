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
    /// creates a <see cref="UiScrollViewer"/>. Unless the viewer has its own value, it obtains
    /// this setting from the template owner in the same way that WPF forwards its standard
    /// scrolling attached properties.
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
    [AttachedPropertyBrowsableForType(typeof(Control))]
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
    /// Identifies the <see cref="GetOverlayMode"/> and <see cref="SetOverlayMode"/> attached
    /// dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ScrollViewerOverlayMode.Overlay"/>. The property can be set
    /// directly on a <see cref="ScrollViewer"/> or on a control whose template creates a
    /// <see cref="UiScrollViewer"/>. Unless that viewer has its own value, it obtains the mode
    /// from the template owner without allowing the setting to leak into independently nested
    /// scrolling controls in the owner's content.
    /// </remarks>
    public static readonly DependencyProperty OverlayModeProperty = DependencyProperty.RegisterAttached(
        "OverlayMode",
        typeof(ScrollViewerOverlayMode),
        typeof(ScrollViewerAssist),
        new FrameworkPropertyMetadata(ScrollViewerOverlayMode.Overlay)
    );

    /// <summary>
    /// Gets how an element's scroll bars are placed relative to its scrolling viewport.
    /// </summary>
    /// <param name="element">
    /// The scroll viewer or template-owning control to query.
    /// </param>
    /// <returns>The configured scroll-bar placement mode.</returns>
    [AttachedPropertyBrowsableForType(typeof(Control))]
    public static ScrollViewerOverlayMode GetOverlayMode(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (ScrollViewerOverlayMode)element.GetValue(OverlayModeProperty);
    }

    /// <summary>
    /// Sets how an element's scroll bars are placed relative to its scrolling viewport.
    /// </summary>
    /// <param name="element">
    /// The scroll viewer or template-owning control to configure.
    /// </param>
    /// <param name="value">The scroll-bar placement mode.</param>
    public static void SetOverlayMode(DependencyObject element, ScrollViewerOverlayMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(OverlayModeProperty, value);
    }
}
