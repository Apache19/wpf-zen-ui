// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends the WPF <see cref="System.Windows.Controls.Primitives.ToggleButton"/> with Fluent visuals
/// while preserving the native checked, unchecked, and indeterminate state model. Its default
/// template implements the primary icon-slot contract defined by <see cref="IconAssist"/>.
/// </summary>
/// <remarks>
/// The icon is part of the default template rather than the button content. Applications can therefore
/// replace or bind <see cref="System.Windows.Controls.ContentControl.Content"/> independently without
/// reconstructing an icon-and-label layout.
/// </remarks>
public class ToggleButton : System.Windows.Controls.Primitives.ToggleButton
{
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
