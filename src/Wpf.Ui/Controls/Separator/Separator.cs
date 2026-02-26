// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class Separator : System.Windows.Controls.Separator
{
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
                default(Thickness),
                null,
                static (d, baseValue) =>
                {
                    // Apply different default margins based on orientation.
                    // This coercion only takes effect when the base value
                    // is the default (i.e. Margin has not been explicitly set).
                    var separator = (Separator)d;
                    if ((Thickness)baseValue == default)
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
        DependencyProperty.RegisterAttached(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                static (d, e) =>
                {
                    if (d is Separator separator)
                    {
                        separator.CoerceValue(MarginProperty);
                    }
                }
            )
        );

    /// <summary>Helper for getting <see cref="OrientationProperty"/> from <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to read <see cref="OrientationProperty"/> from.</param>
    /// <returns>Orientation property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static Orientation GetOrientation(DependencyObject obj)
    {
        return (Orientation)obj.GetValue(OrientationProperty);
    }

    /// <summary>Helper for setting <see cref="OrientationProperty"/> on <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to set <see cref="OrientationProperty"/> on.</param>
    /// <param name="value">Orientation property value.</param>
    public static void SetOrientation(DependencyObject obj, Orientation value)
    {
        obj.SetValue(OrientationProperty, value);
    }

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
        DependencyProperty.RegisterAttached(
            nameof(DefaultMarginHorizontal),
            typeof(Thickness),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                default(Thickness),
                static (d, e) =>
                {
                    if (d is Separator separator)
                    {
                        separator.CoerceValue(MarginProperty);
                    }
                }
            )
        );

    /// <summary>Helper for getting <see cref="DefaultMarginHorizontalProperty"/> from <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to read <see cref="DefaultMarginHorizontalProperty"/> from.</param>
    /// <returns>DefaultMarginHorizontal property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static Thickness GetDefaultMarginHorizontal(DependencyObject obj)
    {
        return (Thickness)obj.GetValue(DefaultMarginHorizontalProperty);
    }

    /// <summary>Helper for setting <see cref="DefaultMarginHorizontalProperty"/> on <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to set <see cref="DefaultMarginHorizontalProperty"/> on.</param>
    /// <param name="value">DefaultMarginHorizontal property value.</param>
    public static void SetDefaultMarginHorizontal(DependencyObject obj, Thickness value)
    {
        obj.SetValue(DefaultMarginHorizontalProperty, value);
    }

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
        DependencyProperty.RegisterAttached(
            nameof(DefaultMarginVertical),
            typeof(Thickness),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                default(Thickness),
                (d, e) =>
                {
                    if (d is Separator separator)
                    {
                        separator.CoerceValue(MarginProperty);
                    }
                }
            )
        );

    /// <summary>Helper for getting <see cref="DefaultMarginVerticalProperty"/> from <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to read <see cref="DefaultMarginVerticalProperty"/> from.</param>
    /// <returns>DefaultMarginVertical property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static Thickness GetDefaultMarginVertical(DependencyObject obj)
    {
        return (Thickness)obj.GetValue(DefaultMarginVerticalProperty);
    }

    /// <summary>Helper for setting <see cref="DefaultMarginVerticalProperty"/> on <paramref name="obj"/>.</summary>
    /// <param name="obj"><see cref="DependencyObject"/> to set <see cref="DefaultMarginVerticalProperty"/> on.</param>
    /// <param name="value">DefaultMarginVertical property value.</param>
    public static void SetDefaultMarginVertical(DependencyObject obj, Thickness value)
    {
        obj.SetValue(DefaultMarginVerticalProperty, value);
    }

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
}