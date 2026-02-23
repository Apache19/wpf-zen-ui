// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(TreeViewItem))]
public class TreeView : System.Windows.Controls.TreeView
{
    static TreeView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TreeView),
            new FrameworkPropertyMetadata(typeof(TreeView))
        );
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new TreeViewItem();
    }
}