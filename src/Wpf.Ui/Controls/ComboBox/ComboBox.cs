// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class ComboBox : System.Windows.Controls.ComboBox
{
    static ComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(typeof(ComboBox))
        );
    }
}