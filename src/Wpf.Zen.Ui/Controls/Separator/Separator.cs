// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

public class Separator : System.Windows.Controls.Separator
{
    // Sentinel value used to detect whether Margin has been explicitly set by the user.
    // Since double.MaxValue is an extreme value that no reasonable layout would use,
    // it serves as a reliable "unset" marker.
    private static readonly Thickness SentinelMargin = new(
        double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue
    );

    static Separator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Separator),
            new FrameworkPropertyMetadata(typeof(Separator))
        );

        // Override the metadata of the Margin property to provide
        // orientation-aware default margin values.
        MarginProperty.OverrideMetadata(
            typeof(Separator),
            new FrameworkPropertyMetadata(
                SentinelMargin,
                null,
                static (d, baseValue) =>
                {
                    // Apply different default margins based on orientation.
                    // This coercion only takes effect when the base value
                    // is the default (i.e. Margin has not been explicitly set).
                    var separator = (Separator)d;
                    if ((Thickness)baseValue == SentinelMargin)
                    {
                        return separator.Orientation switch
                        {
                            Orientation.Horizontal => separator.DefaultMarginHorizontal,
                            Orientation.Vertical   => separator.DefaultMarginVertical,
                            _                      => baseValue
                        };
                    }

                    // Preserve the original value if a custom margin is provided
                    // or if the orientation does not match.
                    return baseValue;
                }
            )
        );
    }

    /// <summary>
    /// Identifies the <see cref="Orientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                OnAffectsMarginPropertyChanged
            )
        );

    /// <summary>
    /// Gets or sets the orientation of the <see cref="Separator"/>.
    /// </summary>
    /// <remarks>
    /// The orientation affects layout-related behavior, such as the
    /// applied default margin and visual arrangement.
    /// </remarks>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DefaultMarginHorizontal"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DefaultMarginHorizontalProperty =
        DependencyProperty.Register(
            nameof(DefaultMarginHorizontal),
            typeof(Thickness),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                default(Thickness),
                OnAffectsMarginPropertyChanged
            )
        );

    /// <summary>
    /// Gets or sets the default <see cref="Thickness"/> used as the margin
    /// when the <see cref="Separator"/> is oriented horizontally.
    /// </summary>
    /// <remarks>
    /// This value is applied only when the <see cref="FrameworkElement.Margin"/> property
    /// has not been explicitly set.
    /// </remarks>
    public Thickness DefaultMarginHorizontal
    {
        get => (Thickness)GetValue(DefaultMarginHorizontalProperty);
        set => SetValue(DefaultMarginHorizontalProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DefaultMarginVertical"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DefaultMarginVerticalProperty =
        DependencyProperty.Register(
            nameof(DefaultMarginVertical),
            typeof(Thickness),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                default(Thickness),
                OnAffectsMarginPropertyChanged
            )
        );

    /// <summary>
    /// Gets or sets the default <see cref="Thickness"/> used as the margin
    /// when the <see cref="Separator"/> is oriented vertically.
    /// </summary>
    /// <remarks>
    /// This value is applied only when the <see cref="FrameworkElement.Margin"/> property
    /// has not been explicitly set.
    /// </remarks>
    public Thickness DefaultMarginVertical
    {
        get => (Thickness)GetValue(DefaultMarginVerticalProperty);
        set => SetValue(DefaultMarginVerticalProperty, value);
    }

    private static void OnAffectsMarginPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Separator separator)
        {
            separator.CoerceValue(MarginProperty);
        }
    }
}