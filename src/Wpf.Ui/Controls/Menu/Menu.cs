// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Theming;

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

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        // The WPF UI Menu is responsible for ensuring that its child MenuItem
        // containers receive the correct default styling within this theme system.
        //
        // In particular, native WPF MenuItem instances do not automatically resolve
        // styles from this library's Generic.xaml (especially in scenarios without
        // an Application resource scope). Therefore, we explicitly apply the
        // library's default style when necessary.
        if (item is System.Windows.Controls.MenuItem menuItem)
        {
            // Only apply styling to standard (unmodified) MenuItem instances.
            // This preserves user-defined styles and avoids overriding custom
            // MenuItem subclasses or explicitly styled instances.
            if (MenuItem.IsStandardMenuItem(menuItem))
            {
                MenuItem.ApplyThemeStyling(menuItem, StyleKeys.MenuItemStyle);
            }

            // Delegate remaining preparation logic to the base implementation.
            base.PrepareContainerForItemOverride(element, item);
            return;
        }

        // Handle Separator instances separately.
        // The base Menu implementation assigns a system-level style key to menu
        // Separators, which bypasses this library's theme resources. We redirect
        // their style resolution to the library's ComponentResourceKey instead.
        MenuItem.PrepareSeparator(element, item);
    }
}