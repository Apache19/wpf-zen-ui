// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

[DefaultProperty("Content")]
public class PlaceholderControl : ContentControl
{
    static PlaceholderControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PlaceholderControl),
            new FrameworkPropertyMetadata(typeof(PlaceholderControl))
        );

        IsHitTestVisibleProperty.OverrideMetadata(
            typeof(PlaceholderControl),
            new UIPropertyMetadata(false)
        );

        IsTabStopProperty.OverrideMetadata(
            typeof(PlaceholderControl),
            new FrameworkPropertyMetadata(false)
        );

        FocusableProperty.OverrideMetadata(
            typeof(PlaceholderControl),
            new FrameworkPropertyMetadata(false)
        );
    }
}