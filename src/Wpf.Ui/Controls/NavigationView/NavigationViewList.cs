// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Hosts the items displayed by a <see cref="NavigationView"/> or <see cref="NavigationViewItem"/>.
/// </summary>
/// <remarks>
/// Unlike a general-purpose <see cref="ItemsControl"/>, this control understands that a navigation
/// menu can temporarily contain structural elements or other objects alongside <see cref="NavigationViewItem"/>
/// containers. Until the list owns navigation-item generation, it therefore applies navigation-item
/// container styles only to existing <see cref="NavigationViewItem"/> containers. Keeping this behavior
/// in a dedicated list also provides a single container-generation boundary for future data-driven
/// navigation improvements.
/// </remarks>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public class NavigationViewList : ItemsControl
{
    /// <summary>
    /// Determines whether this list should apply its item-container style or style selector to the
    /// specified item.
    /// </summary>
    /// <param name="container">The element used to display <paramref name="item"/>.</param>
    /// <param name="item">The item being prepared for display.</param>
    /// <returns>
    /// The result of the base WPF implementation when <paramref name="container"/> is a
    /// <see cref="NavigationViewItem"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// WPF validates an item-container style's target type before assigning it. Because this list does not
    /// yet generate <see cref="NavigationViewItem"/> containers for arbitrary data, structural elements and
    /// other directly supplied objects can retain incompatible container types. Restricting style application
    /// by the actual container type prevents those heterogeneous items from receiving a navigation-item style.
    /// When navigation-item generation is introduced, generated <see cref="NavigationViewItem"/> containers
    /// will naturally pass the same check without changing this contract.
    /// </remarks>
    protected override bool ShouldApplyItemContainerStyle(DependencyObject container, object item)
    {
        return container is NavigationViewItem
            && base.ShouldApplyItemContainerStyle(container, item);
    }
}
