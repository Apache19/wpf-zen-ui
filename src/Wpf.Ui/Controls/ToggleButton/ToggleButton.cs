// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Ui.Controls;

/// <summary>
/// Extends the WPF <see cref="System.Windows.Controls.Primitives.ToggleButton"/> with a leading
/// <see cref="Icon"/> while preserving the native checked, unchecked, and indeterminate state model.
/// </summary>
/// <remarks>
/// The icon is part of the default template rather than the button content. Applications can therefore
/// replace or bind <see cref="System.Windows.Controls.ContentControl.Content"/> independently without
/// reconstructing an icon-and-label layout.
/// </remarks>
public class ToggleButton : System.Windows.Controls.Primitives.ToggleButton, IIconControl
{
    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(IconElement),
            typeof(ToggleButton),
            new PropertyMetadata(null, null, IconElement.Coerce)
        );

    /// <summary>
    /// Gets or sets the icon displayed before the toggle button content.
    /// A value of <see langword="null"/> collapses the icon presenter and removes its reserved spacing.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner(
            typeof(ToggleButton)
        );

    /// <summary>
    /// Gets or sets a value that represents the degree to which the corners of a <see cref="T:System.Windows.Controls.Border" /> are rounded.
    /// </summary>
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    static ToggleButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleButton),
            new FrameworkPropertyMetadata(typeof(ToggleButton))
        );
    }
}