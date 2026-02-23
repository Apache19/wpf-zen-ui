// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

public class TabControl : System.Windows.Controls.TabControl
{
    static TabControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TabControl),
            new FrameworkPropertyMetadata(typeof(TabControl))
        );
    }
}