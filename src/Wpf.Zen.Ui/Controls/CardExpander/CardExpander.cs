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
    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(CardExpander),
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
        typeof(CardExpander),
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
