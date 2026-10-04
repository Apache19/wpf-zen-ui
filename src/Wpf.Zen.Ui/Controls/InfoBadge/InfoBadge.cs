// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays status content with an optional icon inside a rounded border.
/// </summary>
/// <remarks>
/// The default style presents Content and supports native content templates and formatting.
/// IconPlacement selects the side of the content occupied by the optional icon.
/// </remarks>
public class InfoBadge : System.Windows.Controls.ContentControl
{
    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty
        = DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(InfoBadge),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the optional icon displayed alongside Content. The default is null.
    /// Use Padding to control the space around the icon.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Severity"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SeverityProperty
        = DependencyProperty.Register(
            nameof(Severity),
            typeof(BadgeSeverity),
            typeof(InfoBadge),
            new PropertyMetadata(BadgeSeverity.Informational)
        );

    /// <summary>
    /// Gets or sets the semantic status used to choose the badge colors.
    /// The default is <see cref="BadgeSeverity.Informational"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public BadgeSeverity Severity
    {
        get => (BadgeSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty
        = DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(InfoBadge),
            new FrameworkPropertyMetadata(
                new CornerRadius(8),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the radius of the badge border in device-independent units.
    /// The default is 8 on every corner.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconPlacement"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconPlacementProperty = DependencyProperty.Register(
        nameof(IconPlacement),
        typeof(BadgeIconPlacement),
        typeof(InfoBadge),
        new PropertyMetadata(BadgeIconPlacement.Left),
        IsValidIconPlacement
    );

    /// <summary>
    /// Gets or sets the side of Content occupied by Icon. The default is Left.
    /// Positions follow the inherited FlowDirection.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public BadgeIconPlacement IconPlacement
    {
        get => (BadgeIconPlacement)GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    /// <summary>
    /// Accepts only the horizontal positions supported by the badge template.
    /// </summary>
    private static bool IsValidIconPlacement(object value)
    {
        return value is BadgeIconPlacement.Left or BadgeIconPlacement.Right;
    }

    static InfoBadge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(InfoBadge),
            new FrameworkPropertyMetadata(typeof(InfoBadge))
        );
    }
}
