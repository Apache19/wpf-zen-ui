// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Coordinates selection state owned by <see cref="NavigationViewItem"/>.</summary>
internal static class NavigationViewItemTemplateBehavior
{
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
}
