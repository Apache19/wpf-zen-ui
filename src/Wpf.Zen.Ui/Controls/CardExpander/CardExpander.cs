// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Inherited from the <see cref="System.Windows.Controls.Expander"/> control which can hide the collapsible content.
/// </summary>
public class CardExpander : System.Windows.Controls.Expander
{
    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(CardExpander),
        new PropertyMetadata(new CornerRadius(4))
    );

    /// <summary>Identifies the <see cref="ContentPadding"/> dependency property.</summary>
    public static readonly DependencyProperty ContentPaddingProperty = DependencyProperty.Register(
        nameof(ContentPadding),
        typeof(Thickness),
        typeof(CardExpander),
        new FrameworkPropertyMetadata(
            default(Thickness),
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
        )
    );

    static CardExpander()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CardExpander),
            new FrameworkPropertyMetadata(typeof(CardExpander))
        );
    }

    /// <summary>
    /// Gets or sets the radius applied to the expander's outer corners.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets content padding Property
    /// </summary>
    [Bindable(true)]
    [Category("Layout")]
    public Thickness ContentPadding
    {
        get { return (Thickness)GetValue(ContentPaddingProperty); }
        set { SetValue(ContentPaddingProperty, value); }
    }
}
