// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends the WPF <see cref="System.Windows.Controls.Primitives.ToggleButton"/> with Fluent visuals
/// while preserving the native checked, unchecked, and indeterminate state model. Its default
/// template presents an <see cref="Icon"/> slot.
/// </summary>
/// <remarks>
/// The icon is part of the default template rather than the button content. Applications can therefore
/// replace or bind <see cref="System.Windows.Controls.ContentControl.Content"/> independently without
/// reconstructing an icon-and-label layout.
/// </remarks>
public class ToggleButton : System.Windows.Controls.Primitives.ToggleButton
{
    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(ToggleButton),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>Identifies the <see cref="IconMargin"/> dependency property.</summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(ToggleButton),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

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
