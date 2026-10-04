// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays an icon from any font family described by a FontIconSource.
/// </summary>
/// <remarks>
/// The source owns glyph and font selection. This TextBlock supplies layout and native text
/// presentation; IconPresenter's size, weight and foreground scopes override the corresponding
/// effective text properties while preserving their bindings and local values.
/// </remarks>
public class FontIcon : TextBlock
{
    static FontIcon()
    {
        FontFamilyProperty.OverrideMetadata(
            typeof(FontIcon), new FrameworkPropertyMetadata(null, CoerceFontFamily)
        );

        // IconSize is semantic icon presentation context, whereas FontSize remains the native
        // rendering mechanism of this font-backed implementation. Coercion lets the semantic value
        // win without replacing a local value or binding on FontSize; clearing the icon-size scope
        // immediately reveals the original WPF value again.
        FontSizeProperty.OverrideMetadata(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                static (d, baseValue) =>
                {
                    var iconSize = ((FontIcon)d).IconSize;
                    return double.IsNaN(iconSize) ? baseValue! : iconSize;
                }
            )
        );

        FontWeightProperty.OverrideMetadata(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                coerceValueCallback: static (d, baseValue) => ((FontIcon)d).IconWeight ?? (FontWeight)baseValue
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                coerceValueCallback: static (d, baseValue) => ((FontIcon)d).IconForeground ?? baseValue
            )
        );
    }

    private FontIconSource? _effectiveSource;

    private static readonly DependencyProperty ResourceFontFamilyProperty
        = DependencyProperty.Register(
            "ResourceFontFamily", typeof(FontFamily), typeof(FontIcon),
            new PropertyMetadata(null, OnResourceFontChanged)
        );

    /// <summary>
    /// Identifies the <see cref="Source"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SourceProperty
        = DependencyProperty.Register(
            nameof(Source), typeof(FontIconSource), typeof(FontIcon),
            new FrameworkPropertyMetadata(null, OnSourceChanged)
        );

    /// <summary>
    /// Gets or sets the immutable font icon description. Null displays no glyph.
    /// Replace this value to change the icon; one source may be shared by multiple hosts.
    /// </summary>
    public FontIconSource? Source
    {
        get => (FontIconSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Variant"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VariantProperty
        = IconPresenter.VariantProperty.AddOwner(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                IconVariant.Default,
                FrameworkPropertyMetadataOptions.Inherits,
                OnSourceChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited presentation request, interpreted by the source.
    /// Unsupported variants retain the source's own appearance. The default is Default.
    /// </summary>
    public IconVariant Variant
    {
        get => (IconVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconSize"/> dependency property.
    /// </summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// Adding this type as an owner provides a change callback without copying values from an
    /// ancestor or disrupting WPF dependency-property precedence.
    /// </remarks>
    public static readonly DependencyProperty IconSizeProperty
        = IconPresenter.IconSizeProperty.AddOwner(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                double.NaN,
                FrameworkPropertyMetadataOptions.Inherits,
                propertyChangedCallback: static (d, _) =>
                    // FontSize may have any WPF value source. Re-coercion updates only its effective value and
                    // therefore preserves that source for the moment the icon-size scope is removed.
                    d.CoerceValue(FontSizeProperty)
            )
        );

    /// <summary>
    /// Gets or sets the logical size of this icon.
    /// </summary>
    /// <remarks>
    /// A concrete value overrides the effective native
    /// <see cref="System.Windows.Controls.TextBlock.FontSize"/> because it
    /// is an explicit icon-specific request. The default <see cref="double.NaN"/> preserves native
    /// FontSize inheritance, styles, local values, bindings, and animations unchanged.
    /// </remarks>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconWeight"/> dependency property.
    /// </summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// The value is kept separate from ordinary text weight and is mapped to the native font
    /// property only by this font-backed icon implementation.
    /// </remarks>
    public static readonly DependencyProperty IconWeightProperty
        = IconPresenter.IconWeightProperty.AddOwner(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                propertyChangedCallback: static (d, _) =>
                    // As with IconSize, coercing only the effective FontWeight preserves its binding, style,
                    // local value, and animation so native behavior returns when the icon scope is removed.
                    d.CoerceValue(FontWeightProperty)
            )
        );

    /// <summary>
    /// Gets or sets the logical weight of this icon.
    /// </summary>
    /// <remarks>
    /// A concrete value overrides the effective native
    /// <see cref="System.Windows.Controls.TextBlock.FontWeight"/>. The default
    /// <see langword="null"/> value preserves native FontWeight inheritance, styles, local values,
    /// bindings, and animations unchanged. This property does not select between regular and
    /// filled glyphs; use the source's variant selection or <see cref="Variant"/>.
    /// </remarks>
    public FontWeight? IconWeight
    {
        get => (FontWeight?)GetValue(IconWeightProperty);
        set => SetValue(IconWeightProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconForeground"/> dependency property.
    /// </summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// The value remains separate from ordinary text foreground and is mapped to the native text
    /// property only by this font-backed icon implementation.
    /// </remarks>
    public static readonly DependencyProperty IconForegroundProperty
        = IconPresenter.IconForegroundProperty.AddOwner(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits,
                propertyChangedCallback: static (d, _) =>
                    // Coercion changes only Foreground's effective value. Its binding, resource reference,
                    // style, inherited value, or animation remains available as soon as the icon scope ends.
                    d.CoerceValue(ForegroundProperty)
            )
        );

    /// <summary>
    /// Gets or sets the logical foreground brush of this icon.
    /// </summary>
    /// <remarks>
    /// A concrete brush overrides the effective native
    /// <see cref="System.Windows.Controls.TextBlock.Foreground"/>. The default
    /// <see langword="null"/> value preserves native Foreground inheritance, styles, local values,
    /// bindings, dynamic resources, and animations unchanged.
    /// </remarks>
    public Brush? IconForeground
    {
        get => (Brush?)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    /// <summary>
    /// Initializes an empty font icon. Set Source to select a family and glyph.
    /// </summary>
    public FontIcon()
    {
        Focusable         = false;
        TextAlignment     = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>
    /// Initializes a font icon with a reusable description.
    /// </summary>
    /// <param name="source">The font icon description to display.</param>
    public FontIcon(FontIconSource source)
        : this()
    {
        Source = source;
    }

    /// <summary>
    /// Resolves a presentation without modifying the shared source or its binding.
    /// Resource expressions belong to this host so each host follows its own resource scope.
    /// </summary>
    private void UpdateSource()
    {
        _effectiveSource = Source?.WithVariant(Variant);

        var glyph = _effectiveSource?.Glyph ?? string.Empty;
        SetCurrentValue(TextProperty, glyph);

        var resourceKey = _effectiveSource?.FontFamilyResourceKey;
        if (resourceKey is null)
        {
            ClearValue(ResourceFontFamilyProperty);
        }
        else
        {
            SetResourceReference(ResourceFontFamilyProperty, resourceKey);
        }

        CoerceValue(FontFamilyProperty);
    }

    /// <summary>
    /// Uses a scoped font when available and the source's font otherwise.
    /// Clearing Source restores the ordinary inherited font.
    /// </summary>
    private static object CoerceFontFamily(DependencyObject sender, object baseValue)
    {
        var icon         = (FontIcon)sender;
        var resourceFont = (FontFamily?)icon.GetValue(ResourceFontFamilyProperty);
        return resourceFont ?? icon._effectiveSource?.FontFamily ?? baseValue;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (FontIcon)sender;
        icon.UpdateSource();
    }

    private static void OnResourceFontChanged(
        DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        sender.CoerceValue(FontFamilyProperty);
    }
}