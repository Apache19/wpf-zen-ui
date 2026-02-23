// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class Slider : System.Windows.Controls.Slider
{
    static Slider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Slider),
            new FrameworkPropertyMetadata(typeof(Slider))
        );
    }
}