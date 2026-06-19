// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Documents;
using Wpf.Ui.Theming;

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
    // Non-null design-system properties intentionally take precedence over their
    // low-level WPF counterparts, regardless of those values' sources.

    private static readonly FieldInfo? DefaultValueField = typeof(PropertyMetadata).GetField(
        "_defaultValue",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo? CoerceValueCallbackField = typeof(PropertyMetadata).GetField(
        "_coerceValueCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    static TextBlock()
    {
        #pragma warning disable IDE0002

        // Modify the effective metadata used by each WPF FontSize owner. This establishes
        // Fluent's 14px default without consuming OverrideMetadata for application types.

        SetMetadataDefaultValue(
            TextElement.FontSizeProperty,
            typeof(TextElement),
            14d
        );
        SetMetadataDefaultValue(
            System.Windows.Controls.Control.FontSizeProperty,
            typeof(System.Windows.Controls.Control),
            14d
        );
        SetMetadataDefaultValue(
            System.Windows.Controls.TextBlock.FontSizeProperty,
            typeof(System.Windows.Controls.TextBlock),
            14d
        );
        SetMetadataDefaultValue(
            System.Windows.Controls.AccessText.FontSizeProperty,
            typeof(System.Windows.Controls.AccessText),
            14d
        );
        SetMetadataDefaultValue(
            System.Windows.Controls.Page.FontSizeProperty,
            typeof(System.Windows.Controls.Page),
            14d
        );

        // Replace WPF's black Foreground metadata default with null as a sentinel.
        // CoerceForeground interprets null—including an explicitly supplied null—as a request
        // for DefaultForeground, while every non-null WPF value keeps its normal precedence.
        SetMetadataDefaultValue(
            TextElement.ForegroundProperty,
            typeof(TextElement),
            null
        );

        // Append WPFUI coercion to the native TextBlock metadata.
        // The original callback runs first.

        AppendCoerceValueCallback(
            System.Windows.Controls.TextBlock.FontSizeProperty,
            typeof(System.Windows.Controls.TextBlock),
            CoerceFontSize
        );
        AppendCoerceValueCallback(
            System.Windows.Controls.TextBlock.FontWeightProperty,
            typeof(System.Windows.Controls.TextBlock),
            CoerceFontWeight
        );
        AppendCoerceValueCallback(
            TextElement.ForegroundProperty,
            typeof(System.Windows.Controls.TextBlock),
            CoerceForeground
        );

        #pragma warning restore IDE0002
    }

    private static void SetMetadataDefaultValue(
        DependencyProperty property,
        Type ownerType,
        object? defaultValue)
    {
        // Reuse WPF's existing metadata instance instead of overriding it. This keeps
        // the metadata slot available to application-derived controls while changing
        // only the default value used by the specified framework owner.
        DefaultValueField?.SetValue(property.GetMetadata(ownerType), defaultValue);
    }

    private static void AppendCoerceValueCallback(
        DependencyProperty property,
        Type ownerType,
        CoerceValueCallback callback)
    {
        if (CoerceValueCallbackField is null)
        {
            // If the WPF implementation changes and the private field is unavailable,
            // leave the framework metadata untouched rather than replacing it unsafely.
            return;
        }

        // Preserve WPF's callback because it may enforce framework invariants.
        var metadata = property.GetMetadata(ownerType);
        var originalCallback = (CoerceValueCallback?)CoerceValueCallbackField.GetValue(metadata);

        CoerceValueCallbackField.SetValue(
            metadata,
            new CoerceValueCallback(
                (d, baseValue) =>
                {
                    // Step 1: let WPF normalize/coerce its own value.
                    var value = originalCallback?.Invoke(d, baseValue) ?? baseValue;

                    // Step 2: apply the WPF UI semantic-property policy to that result.
                    return callback(d, value);
                }
            )
        );
    }

    #region Coercion Callbacks

    private static object? CoerceFontSize(DependencyObject d, object? baseValue)
    {
        // A resolved non-null typography value is authoritative, even when FontSize
        // has a local value, style, inherited value, binding, or active animation.
        // A null/missing preset component opts out and preserves WPF's base value.
        return d.GetValue(FontTypographyPresetProxyProperty)
            is FontTypographyPreset { FontSize: { } size }
            ? size
            : baseValue;
    }

    private static object? CoerceFontWeight(DependencyObject d, object? baseValue)
    {
        // FontWeight follows the same contract as FontSize so a typography token is
        // applied consistently instead of producing a partially overridden preset.
        return d.GetValue(FontTypographyPresetProxyProperty)
            is FontTypographyPreset { FontWeight: { } weight }
            ? weight
            : baseValue;
    }

    private static object? CoerceForeground(DependencyObject d, object? baseValue)
    {
        // Step 1: a non-null Appearance is an explicit design-system choice and takes
        // precedence over local, styled, inherited, and animated Foreground values.
        if (d.GetValue(AppearanceForegroundProxyProperty) is Brush appearance)
        {
            return appearance;
        }

        // Step 2: preserve every non-null WPF value. Null—either the metadata sentinel
        // or an explicitly supplied value—requests DefaultForeground, whose black metadata
        // default preserves WPF's fallback when the theme resource is unavailable.
        return baseValue ?? d.GetValue(DefaultForegroundProperty);
    }

    #endregion

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
    /// attached property and can also be applied to the native WPF
    /// <see cref="System.Windows.Controls.TextBlock"/>.
    /// </para>
    ///
    /// <para>
    /// When <see cref="FontTypography"/> is set, a resource reference to the
    /// corresponding preset is attached to an internal proxy property so that
    /// the WPF resource system performs resolution and updates automatically.
    /// When non-null, the resolved preset takes precedence over font-size and
    /// font-weight values from local values, styles, inheritance, and animations.
    /// Set <see cref="FontTypography"/> to <c>null</c> to restore normal WPF precedence.
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
            // Step 1: map the semantic token to a stable component-resource key.
            var resourceKey = newTypography.ToResourceKey();

            // Step 2: store a DynamicResource on the proxy. WPF then handles lookup,
            // inheritance scopes, and re-resolution when theme dictionaries change.
            switch (o)
            {
                case FrameworkElement frameworkElement:
                    frameworkElement.SetResourceReference(
                        FontTypographyPresetProxyProperty,
                        resourceKey
                    );
                    break;

                case FrameworkContentElement frameworkContentElement:
                    frameworkContentElement.SetResourceReference(
                        FontTypographyPresetProxyProperty,
                        resourceKey
                    );
                    break;
            }
        }
        else
        {
            // A local null intentionally blocks an inherited token and removes the
            // semantic override, restoring native FontSize/FontWeight precedence.
            o.ClearValue(FontTypographyPresetProxyProperty);
        }

        // Step 3: immediately recompute both low-level properties. The proxy callback
        // repeats this when the resource later resolves or changes.
        o.CoerceValue(TextElement.FontSizeProperty);
        o.CoerceValue(TextElement.FontWeightProperty);
    }

    /// <summary>
    /// Helper for getting <see cref="FontTypographyProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element"><see cref="DependencyObject"/> to read <see cref="FontTypographyProperty"/> from.</param>
    /// <returns>FontTypography property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static FontTypography? GetFontTypography(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (FontTypography?)element.GetValue(FontTypographyProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="FontTypographyProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="FontTypographyProperty"/> on.
    /// </param>
    /// <param name="value">FontTypography property value.</param>
    public static void SetFontTypography(DependencyObject element, FontTypography? value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(FontTypographyProperty, value);
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
    /// to a valid brush, it takes precedence over local, styled, inherited, and
    /// animated <see cref="System.Windows.Controls.TextBlock.Foreground"/> values.
    /// </para>
    ///
    /// <para>
    /// When <see cref="Appearance"/> is <c>null</c>, every non-null WPF
    /// <see cref="System.Windows.Controls.TextBlock.Foreground"/> value keeps its normal
    /// precedence. A null value, whether implicit or explicit, requests the themed
    /// <see cref="DefaultForegroundProperty"/> value.
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
            // Step 1: convert the semantic appearance to its theme brush key.
            var resourceKey = textColor.ToResourceKey();

            // Step 2: attach the DynamicResource to a private proxy so theme changes
            // update the resolved brush without manual resource-tree traversal.
            switch (o)
            {
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
            // A local null is the escape hatch from an inherited Appearance.
            o.ClearValue(AppearanceForegroundProxyProperty);
        }

        // Step 3: switch immediately between semantic Appearance and native WPF
        // Foreground precedence. The proxy callback handles later resource updates.
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
    /// Holds the themed fallback brush used when Foreground is null. Every non-null local,
    /// styled, templated, inherited, or animated Foreground value takes precedence.
    /// </summary>
    public static readonly DependencyProperty DefaultForegroundProperty =
        DependencyProperty.RegisterAttached(
            "DefaultForeground",
            typeof(Brush),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.Inherits,
                static (d, _) =>
                {
                    // Re-run fallback selection whenever the dynamic theme brush resolves
                    // or changes; higher-priority native values remain protected.
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
    /// Private dependency property used to resolve and retain the dynamic brush resource
    /// selected by Appearance. Used only as a resolution proxy, so no accessors are provided.
    /// </summary>
    private static readonly DependencyProperty AppearanceForegroundProxyProperty =
        DependencyProperty.RegisterAttached(
            "AppearanceForegroundProxy",
            typeof(Brush),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                null,
                static (d, _) =>
                {
                    // The resolved Appearance brush changed, so reapply the explicit
                    // semantic override to Foreground.
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
                static (d, _) =>
                {
                    // A typography resource may resolve asynchronously or change with
                    // theme dictionaries; refresh both properties as one semantic unit.
                    d.CoerceValue(TextElement.FontSizeProperty);
                    d.CoerceValue(TextElement.FontWeightProperty);
                }
            )
        );

    #endregion

    public TextBlock()
    {
        // Store the primary theme brush separately. CoerceForeground uses it when the
        // native Foreground pipeline produces null and preserves every non-null value.
        SetResourceReference(DefaultForegroundProperty, TextColor.Primary.ToResourceKey());
    }

    public static void InitializeMetadata()
    {
        // Accessing this method forces execution of TextBlock's type initializer so
        // callers can install the shared native TextBlock metadata before creating UI.
    }
}
