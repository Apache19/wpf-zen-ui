// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides the interaction behavior used by <see cref="NavigationViewItem"/> control templates.
/// </summary>
public static class NavigationViewItemTemplateBehavior
{
    /// <summary>
    /// Identifies the attached property that lets a template render a symbol icon in its filled state.
    /// </summary>
    /// <remarks>
    /// The value inherits through the item's template so a SymbolIcon created by a DataTemplate can
    /// observe it without a reference to a particular visual ancestor. The default item style sets
    /// an explicit false value at every NavigationViewItem boundary to prevent nested items from
    /// inheriting an active ancestor's state.
    /// </remarks>
    public static readonly DependencyProperty IsIconFilledProperty = DependencyProperty.RegisterAttached(
        "IsIconFilled",
        typeof(bool),
        typeof(NavigationViewItemTemplateBehavior),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.Inherits,
            OnIsIconFilledChanged
        )
    );

    /// <summary>
    /// Helper for getting <see cref="IsIconFilledProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsIconFilledProperty"/> from.
    /// </param>
    /// <returns>IsIconFilled property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetIsIconFilled(DependencyObject element)
    {
        return (bool)element.GetValue(IsIconFilledProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="IsIconFilledProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IsIconFilledProperty"/> on.
    /// </param>
    /// <param name="value">IsIconFilled property value.</param>
    public static void SetIsIconFilled(DependencyObject element, bool value)
    {
        element.SetValue(IsIconFilledProperty, value);
    }

    /// <summary>Updates only the selection state owned by <paramref name="item"/>.</summary>
    /// <remarks>
    /// Ancestors deliberately remain inactive. Their presentation-independent branch state is
    /// exposed by <see cref="NavigationViewItem.IsDescendantActive"/> and interpreted by each
    /// template. Conflating the two states used to make the meaning of IsActive depend on whether
    /// descendants happened to be presented inline or through a detached menu.
    /// </remarks>
    internal static void SetIsActive(NavigationViewItem item, bool isActive)
    {
        item.SetValue(NavigationViewItem.IsActivePropertyKey, isActive);

        if (
            isActive
         && item is
            {
                UsesInlineExpansion: true,
                NavigationViewItemParent: NavigationViewItem parentItem,
            }
        )
        {
            // Inline activation still reveals the selected row. This changes expansion state, not
            // selection state, and therefore does not recreate the former ancestor activation.
            parentItem.SetCurrentValue(NavigationViewItem.IsExpandedProperty, true);
        }
    }

    internal static void UpdateIcon(NavigationViewItem item, SymbolIcon? oldIcon = null)
    {
        if (oldIcon is not null && !ReferenceEquals(oldIcon, item.Icon))
        {
            oldIcon.ClearValue(SymbolIcon.IsFilledRequestedProperty);
        }

        if (item.Icon is SymbolIcon symbolIcon)
        {
            symbolIcon.SetCurrentValue(
                SymbolIcon.IsFilledRequestedProperty,
                GetIsIconFilled(item)
            );
        }
    }

    private static void OnIsIconFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SymbolIcon symbolIcon)
        {
            // DataTemplate-created symbols receive the state through property inheritance. Keep it
            // separate from Filled so application values and bindings remain the base value that
            // WPF's coercion system restores whenever navigation no longer requests a filled icon.
            symbolIcon.SetCurrentValue(SymbolIcon.IsFilledRequestedProperty, (bool)e.NewValue);
            return;
        }

        if (d is NavigationViewItem item)
        {
            UpdateIcon(item);
        }
    }
}
