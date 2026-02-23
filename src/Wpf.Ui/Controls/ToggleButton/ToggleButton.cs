// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

public class ToggleButton : System.Windows.Controls.Primitives.ToggleButton
{
    static ToggleButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleButton),
            new FrameworkPropertyMetadata(typeof(ToggleButton))
        );
    }
}