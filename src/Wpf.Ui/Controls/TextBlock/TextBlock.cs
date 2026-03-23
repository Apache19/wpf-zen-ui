// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;
using Wpf.Ui.Theming;

#pragma warning disable WPF0011 // Containing type should be used as registered owner
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extended <see cref="System.Windows.Controls.TextBlock"/> that integrates with the library's
/// typography and appearance resources.
/// </summary>
/// <remarks>
/// This control supports two mechanisms for applying design-system typography:
/// <para>
/// - The <see cref="FontTypography"/> enum which is mapped to a resource key and resolved
///   at runtime to a <see cref="FontTypographyPreset"/>.
/// </para>
/// <para>
/// - An internal resource-backed preset is resolved via a private puppet dependency property
///   so the framework's resource system (DynamicResource) performs resolution and updates.
/// </para>
/// <para>
/// The resolved preset is used to coerce <see cref="System.Windows.Controls.TextBlock.FontSizeProperty"/>
/// and <see cref="System.Windows.Controls.TextBlock.FontWeight"/>.
/// </para>
/// <para>
/// The <see cref="Appearance"/> property maps to brush resources and is applied via a resource reference.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:TextBlock FontTypography="Body" Appearance="Primary" Text="Hello, world!" /&gt;
/// </code>
/// </example>
public class TextBlock : System.Windows.Controls.TextBlock
{
    static TextBlock()
    {
        TextElement.FontSizeProperty.OverrideMetadata(
            typeof(TextElement),
            new FrameworkPropertyMetadata(
                14d,
                null,
                CoerceFontSize
            )
        );

        FontSizeProperty.OverrideMetadata(
            typeof(System.Windows.Controls.TextBlock),
            new FrameworkPropertyMetadata(
                14d,
                null,
                CoerceFontSize
            )
        );

        Control.FontSizeProperty.OverrideMetadata(
            typeof(Control),
            new FrameworkPropertyMetadata(
                14d,
                null,
                CoerceFontSize
            )
        );

        TextElement.FontWeightProperty.OverrideMetadata(
            typeof(TextElement),
            new FrameworkPropertyMetadata(
                FontWeights.Regular,
                null,
                CoerceFontWeight
            )
        );

        FontWeightProperty.OverrideMetadata(
            typeof(System.Windows.Controls.TextBlock),
            new FrameworkPropertyMetadata(
                FontWeights.Regular,
                null,
                CoerceFontWeight
            )
        );

        Control.FontWeightProperty.OverrideMetadata(
            typeof(Control),
            new FrameworkPropertyMetadata(
                FontWeights.Regular,
                null,
                CoerceFontWeight
            )
        );

        TextElement.ForegroundProperty.OverrideMetadata(
            typeof(TextElement),
            new FrameworkPropertyMetadata(
                null,
                null,
                CoerceForeground
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(System.Windows.Controls.TextBlock),
            new FrameworkPropertyMetadata(
                null,
                null,
                CoerceForeground
            )
        );

        Control.ForegroundProperty.OverrideMetadata(
            typeof(Control),
            new FrameworkPropertyMetadata(
                null,
                null,
                CoerceForeground
            )
        );
    }

    #region Coercion Callbacks

    private static object? CoerceFontSize(DependencyObject d, object? baseValue)
    {
        // Typography property takes precedence:
        var preset = d.GetValue(FontTypographyPresetProxyProperty) as FontTypographyPreset;
        if (preset?.FontSize is { } size)
        {
            return size;
        }

        return baseValue;
    }

    private static object? CoerceFontWeight(DependencyObject d, object? baseValue)
    {
        var preset = d.GetValue(FontTypographyPresetProxyProperty) as FontTypographyPreset;
        if (preset?.FontWeight is { } weight)
        {
            return weight;
        }

        return baseValue;
    }

    private static object CoerceForeground(DependencyObject d, object? baseValue)
    {
        // Appearance property takes precedence:
        // If the Appearance property is set and resolves to a Brush, use it.
        if (d.GetValue(AppearanceForegroundProxyProperty) is Brush appearance)
        {
            return appearance;
        }

        // Otherwise, if Foreground already has a valid value
        // (set via styles, inheritance, or other mechanisms), keep it.
        if (baseValue is Brush)
        {
            return baseValue;
        }

        // Otherwise, try to resolve the dynamic theme resource referenced by
        // the implicit default foreground resource property.
        if (d.GetValue(DefaultForegroundProperty) is Brush defaultForeground)
        {
            return defaultForeground;
        }

        // Finally, fall back to WPF's default primary text foreground brush.
        return Brushes.Black;
    }

    #endregion

    public static void InitializeMetadata()
    {
        // Forces execution of TextBlock's type initializer.
    }

    #region Attached FontTypography

    /// <summary>Identifies the <see cref="FontTypography"/> dependency property.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="FontTypography"/> defines a logical typography token (for example
    /// <c>Body</c>, <c>Caption</c>, etc.) which is resolved to a <see cref="FontTypographyPreset"/>
    /// resource by the WPFUI theming system. The preset determines font-related properties
    /// such as <see cref="System.Windows.Controls.TextBlock.FontSize"/> and
    /// <see cref="System.Windows.Controls.TextBlock.FontWeight"/>.
    /// </para>
    ///
    /// <para>
    /// Although declared on <see cref="TextBlock"/>, this property is an
    /// <b>attached property</b> and can also be applied to the native WPF
    /// <see cref="System.Windows.Controls.TextBlock"/>, allowing standard
    /// TextBlock controls to participate in WPFUI typography.
    /// </para>
    ///
    /// <para>
    /// When <see cref="FontTypography"/> is set, a resource reference to the
    /// corresponding preset is attached to an internal proxy property so that
    /// the WPF resource system performs resolution and updates automatically.
    /// The resolved preset is used to coerce font-size and font-weight values
    /// so that typography-defined values take precedence over local changes.
    /// </para>
    ///
    /// Example:
    /// <example>
    /// <code lang="xml">
    /// &lt;TextBlock Text="Hello, world!" ui:TextBlock.FontTypography="Body" /&gt;
    /// &lt;ui:TextBlock Text="Hello, world!" FontTypography="Body" /&gt;
    /// </code>
    /// </example>
    /// </remarks>
    public static readonly DependencyProperty FontTypographyProperty =
        DependencyProperty.RegisterAttached(
            nameof(FontTypography),
            typeof(FontTypography?),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                OnFontTypographyChanged
            )
        );

    private static void OnFontTypographyChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is FontTypography newTypography)
        {
            var resourceKey = newTypography.ToResourceKey();

            // Use WPF resource reference mechanism to resolve and cache the preset.
            // This leverages the dependency property resource resolution pipeline,
            // including global indexing and caching optimizations, instead of
            // performing per-instance TryFindResource lookups that would traverse
            // the resource tree and introduce unnecessary overhead.
            switch (o)
            {
                case FrameworkElement fe:
                    fe.SetResourceReference(FontTypographyPresetProxyProperty, resourceKey);
                    break;

                case FrameworkContentElement fce:
                    fce.SetResourceReference(FontTypographyPresetProxyProperty, resourceKey);
                    break;
            }
        }
        else
        {
            // Clear any puppet resource reference
            o.ClearValue(FontTypographyPresetProxyProperty);
        }

        // Re-evaluate coerced values so when FontTypography is set, the
        // CoerceValueCallbacks installed on FontSize/FontWeight will take effect
        // and prevent local changes from overriding typography-defined values.
        o.CoerceValue(TextElement.FontSizeProperty);
        o.CoerceValue(TextElement.FontWeightProperty);
    }

    /// <summary>
    /// Helper for getting <see cref="FontTypographyProperty"/> from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to read <see cref="FontTypographyProperty"/> from.
    /// </param>
    /// <returns>
    /// FontTypography property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static FontTypography? GetFontTypography(DependencyObject obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return (FontTypography?)obj.GetValue(FontTypographyProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="FontTypographyProperty"/> on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to set <see cref="FontTypographyProperty"/> on.
    /// </param>
    /// <param name="value">
    /// FontTypography property value.
    /// </param>
    public static void SetFontTypography(DependencyObject obj, FontTypography? value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        obj.SetValue(FontTypographyProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="FontTypography"/> of the text.
    /// </summary>
    public FontTypography? FontTypography
    {
        get => (FontTypography?)GetValue(FontTypographyProperty);
        set => SetValue(FontTypographyProperty, value);
    }

    #endregion

    #region Attached Appearance

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="Appearance"/> defines a logical text appearance (for example
    /// <c>Primary</c>, <c>Secondary</c>, etc.) which is resolved to a themed
    /// foreground brush by the WPFUI theming system.
    /// </para>
    ///
    /// <para>
    /// Although declared on <see cref="TextBlock"/>, this property is an
    /// <b>attached property</b> and can also be applied to the native WPF
    /// <see cref="System.Windows.Controls.TextBlock"/>, allowing standard
    /// TextBlock controls to participate in WPFUI text theming.
    /// </para>
    ///
    /// <para>
    /// When <see cref="Appearance"/> is explicitly set (non-null) and resolves
    /// to a valid brush, it takes <b>highest precedence</b> during foreground
    /// resolution and overrides values provided by styles, inheritance, or
    /// local <see cref="System.Windows.Controls.TextBlock.Foreground"/>.
    /// </para>
    ///
    /// <para>
    /// When <see cref="Appearance"/> is <c>null</c>, it does not participate in
    /// foreground selection and the normal WPF precedence rules apply.
    /// </para>
    ///
    /// Example:
    /// <example>
    /// <code>
    /// &lt;TextBlock Text="Hello, world!" ui:TextBlock.Appearance="Primary" /&gt;
    /// &lt;ui:TextBlock Text="Hello, world!" Appearance="Primary" /&gt;
    /// </code>
    /// </example>
    /// </remarks>
    public static readonly DependencyProperty AppearanceProperty =
        DependencyProperty.RegisterAttached(
            nameof(Appearance),
            typeof(TextColor?),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                OnAppearanceChanged
            )
        );

    private static void OnAppearanceChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is TextColor textColor)
        {
            var resourceKey = textColor.ToResourceKey();

            switch (o)
            {
                // Similar to OnFontTypographyChanged, attach themed color resource reference to a proxy property,
                // allowing the WPF resource system to handle updates automatically.
                case FrameworkElement fe:
                    fe.SetResourceReference(AppearanceForegroundProxyProperty, resourceKey);
                    break;

                case FrameworkContentElement fc:
                    fc.SetResourceReference(AppearanceForegroundProxyProperty, resourceKey);
                    break;
            }
        }
        else
        {
            o.ClearValue(AppearanceForegroundProxyProperty);
        }

        o.CoerceValue(TextElement.ForegroundProperty);
    }

    /// <summary>
    /// Gets or sets the color of the text.
    /// </summary>
    public TextColor? Appearance
    {
        get => (TextColor?)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    #endregion

    #region Attached DefaultForeground

    /// <summary>
    /// Attached property to hold a resource reference to a Brush that should be
    /// used as the default Foreground when the control's Foreground is not set.
    /// </summary>
    public static readonly DependencyProperty DefaultForegroundProperty =
        DependencyProperty.RegisterAttached(
            "DefaultForeground",
            typeof(Brush),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                static (d, _) =>
                {
                    // When the attached resource ref resolves/changes, coerce Foreground so the new value is used.
                    d.CoerceValue(TextElement.ForegroundProperty);
                }
            )
        );

    /// <summary>
    /// Helper for setting <see cref="DefaultForegroundProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="DefaultForegroundProperty"/> on.
    /// </param>
    /// <param name="value">DefaultForeground property value.</param>
    public static void SetDefaultForeground(DependencyObject element, Brush? value)
    {
        element.SetValue(DefaultForegroundProperty, value);
    }

    /// <summary>
    /// Helper for getting <see cref="DefaultForegroundProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="DefaultForegroundProperty"/> from.
    /// </param>
    /// <returns>DefaultForeground property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(System.Windows.Controls.TextBlock))]
    public static Brush? GetDefaultForeground(DependencyObject element)
    {
        return (Brush?)element.GetValue(DefaultForegroundProperty);
    }

    #endregion

    #region Attached AppearanceForegroundProxy

    /// <summary>
    /// A Puppet dependency property that is used to hold the foreground resource resolved from the Appearance property.
    /// Used only as a resolution proxy, so no accessors are provided."
    /// </summary>
    private static readonly DependencyProperty AppearanceForegroundProxyProperty =
        DependencyProperty.RegisterAttached(
            "AppearanceForegroundProxy",
            typeof(Brush),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                static (d, _) =>
                {
                    d.CoerceValue(TextElement.ForegroundProperty);
                }
            )
        );

    /// <summary>
    /// Helper for getting <see cref="AppearanceProperty"/> from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to read <see cref="AppearanceProperty"/> from.
    /// </param>
    /// <returns>
    /// Appearance property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static TextColor? GetAppearance(DependencyObject obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return (TextColor?)obj.GetValue(AppearanceProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="AppearanceProperty"/> on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to set <see cref="AppearanceProperty"/> on.
    /// </param>
    /// <param name="value">
    /// Appearance property value.
    /// </param>
    public static void SetAppearance(DependencyObject obj, TextColor? value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        obj.SetValue(AppearanceProperty, value);
    }

    #endregion

    #region Attached FontTypographyPresetProxy

    /// <summary>
    /// Private Puppet dependency property used to hold a ResourceReference to a FontTypographyPreset.
    /// </summary>
    private static readonly DependencyProperty FontTypographyPresetProxyProperty =
        DependencyProperty.RegisterAttached(
            "FontTypographyPresetProxy",
            typeof(FontTypographyPreset),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                static (d, _) =>
                {
                    d.CoerceValue(TextElement.FontSizeProperty);
                    d.CoerceValue(TextElement.FontWeightProperty);
                }
            )
        );

    #endregion

    public TextBlock()
    {
        SetResourceReference(DefaultForegroundProperty, TextColor.Primary.ToResourceKey());
    }
}