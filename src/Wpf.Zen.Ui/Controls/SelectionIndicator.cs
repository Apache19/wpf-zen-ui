// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides the inheritable selection indicator setting used by item containers.
/// </summary>
public static class SelectionIndicator
{
    /// <summary>
    /// Identifies the <c>IsSelectionIndicatorVisible</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectionIndicatorVisibleProperty =
        DependencyProperty.RegisterAttached(
            "IsSelectionIndicatorVisible",
            typeof(bool),
            typeof(SelectionIndicator),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets whether selected item containers display the accent-colored selection indicator.
    /// </summary>
    public static bool GetIsSelectionIndicatorVisible(DependencyObject element)
    {
        return (bool)element.GetValue(IsSelectionIndicatorVisibleProperty);
    }

    /// <summary>
    /// Sets whether selected item containers display the accent-colored selection indicator.
    /// </summary>
    public static void SetIsSelectionIndicatorVisible(DependencyObject element, bool value)
    {
        element.SetValue(IsSelectionIndicatorVisibleProperty, value);
    }
}
