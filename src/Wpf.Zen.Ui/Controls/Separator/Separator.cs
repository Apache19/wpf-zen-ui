// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a horizontal or vertical divider. Set Margin at the usage site to control spacing.
/// </summary>
public class Separator : System.Windows.Controls.Separator
{
    static Separator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Separator),
            new FrameworkPropertyMetadata(typeof(Separator))
        );
    }

    /// <summary>
    /// Identifies the Orientation dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Separator),
            new FrameworkPropertyMetadata(Orientation.Horizontal)
        );

    /// <summary>
    /// Gets or sets the divider orientation. The default is horizontal.
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
}
