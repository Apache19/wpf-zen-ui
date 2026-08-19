// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.TreeViewItem"/> with the library's visual treatment.
/// Its default template implements the primary icon-slot contract defined by <see cref="IconAssist"/>.
/// </summary>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(TreeViewItem))]
public class TreeViewItem : System.Windows.Controls.TreeViewItem
{
    static TreeViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(typeof(TreeViewItem))
        );
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new TreeViewItem();
    }
}
