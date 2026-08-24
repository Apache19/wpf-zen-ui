// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a header for a group of menu items in a NavigationMenu.
/// </summary>
public class NavigationViewItemHeader : System.Windows.Controls.Control
{
    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(NavigationViewItemHeader),
        new PropertyMetadata(string.Empty)
    );

    static NavigationViewItemHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewItemHeader),
            new FrameworkPropertyMetadata(typeof(NavigationViewItemHeader))
        );

        // A group label has no primary action. Keeping it out of keyboard navigation avoids a dead
        // tab stop while still allowing assistive technology to discover its text in the tree.
        FocusableProperty.OverrideMetadata(
            typeof(NavigationViewItemHeader),
            new FrameworkPropertyMetadata(false)
        );

        System.Windows.Input.KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(NavigationViewItemHeader),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Gets or sets the text presented in the header element.
    /// </summary>
    [Bindable(true)]
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
