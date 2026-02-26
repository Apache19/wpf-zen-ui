// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

public class Menu : System.Windows.Controls.Menu
{
    static Menu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Menu),
            new FrameworkPropertyMetadata(typeof(Menu))
        );
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        if (item is System.Windows.Controls.MenuItem menuItem)
        {
            if (MenuItem.IsStandardMenuItem(menuItem))
            {
                // 原生菜单项在库里（Generic.xaml）无法获得样式，
                // 我们需要为其应用库默认样式
                // todo MenuItem.ApplyThemeStyling(menuItem, StyleKeys.Menu)
            }

            base.PrepareContainerForItemOverride(element, item);
            return;
        }

        MenuItem.PrepareSeparator(element, item);
    }
}