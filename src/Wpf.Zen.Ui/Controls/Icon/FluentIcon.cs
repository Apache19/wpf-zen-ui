// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays one glyph from the Fluent System Icons font family.
/// </summary>
/// <remarks>
/// <para>
/// The family-specific name leaves room for other icon systems to provide peers such as a Material
/// icon element and source. The element derives from <see cref="TextBlock"/> so foreground, size,
/// data context, and the other native text properties inherit without private copies.
/// </para>
/// <para>
/// <see cref="Filled"/> expresses the element's own default. An enclosing
/// <see cref="IconPresenter"/> can override that default with <see cref="Variant"/> for transient
/// control state. WPF coercion combines the two values without detaching a binding or replacing a
/// local value on <see cref="Filled"/>.
/// </para>
/// </remarks>
public class FluentIcon : TextBlock
{
    static FluentIcon()
    {
        // IconSize is semantic icon presentation context, whereas FontSize remains the native
        // rendering mechanism of this font-backed implementation. Coercion lets the semantic value
        // win without replacing a local value or binding on FontSize; clearing the icon-size scope
        // immediately reveals the original WPF value again.
        FontSizeProperty.OverrideMetadata(
            typeof(FluentIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                static (d, baseValue) =>
                {
                    double iconSize = ((FluentIcon)d).IconSize;
                    return double.IsNaN(iconSize) ? baseValue! : iconSize;
                }
            )
        );

        FontWeightProperty.OverrideMetadata(
            typeof(FluentIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                coerceValueCallback: static (d, baseValue) => ((FluentIcon)d).IconWeight ?? (FontWeight)baseValue
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(FluentIcon),
            new FrameworkPropertyMetadata(
                propertyChangedCallback: null,
                coerceValueCallback: static (d, baseValue) => ((FluentIcon)d).IconForeground ?? baseValue
            )
        );
    }

    /// <summary>Identifies the <see cref="Symbol"/> dependency property.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(FluentIcon),
        new FrameworkPropertyMetadata(
            defaultValue: SymbolRegular.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            propertyChangedCallback: static (d, _) => ((FluentIcon)d).UpdateGlyph()
        )
    );

    /// <summary>Gets or sets the Fluent System Icons glyph displayed by this element.</summary>
    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Identifies the <see cref="Filled"/> dependency property.</summary>
    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(FluentIcon),
        new FrameworkPropertyMetadata(
            defaultValue: false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            propertyChangedCallback: static (d, _) =>
            {
                var self = (FluentIcon)d;
                self.UpdateFontFamily();
                self.UpdateGlyph();
            },
            coerceValueCallback: static (d, baseValue) => ((FluentIcon)d).Variant switch
            {
                IconVariant.Regular => false,
                IconVariant.Filled  => true,
                _                   => (bool)baseValue!,
            }
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the filled glyph is used when
    /// <see cref="Variant"/> is <see cref="IconVariant.Default"/>.
    /// </summary>
    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    /// <summary>Identifies the <see cref="IconSize"/> dependency property.</summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// Adding this type as an owner provides a change callback without copying values from an
    /// ancestor or disrupting WPF dependency-property precedence.
    /// </remarks>
    public static readonly DependencyProperty IconSizeProperty = IconPresenter.IconSizeProperty.AddOwner(
        typeof(FluentIcon),
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

    /// <summary>Identifies the <see cref="IconWeight"/> dependency property.</summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// The value is kept separate from ordinary text weight and is mapped to the native font
    /// property only by this font-backed icon implementation.
    /// </remarks>
    public static readonly DependencyProperty IconWeightProperty = IconPresenter.IconWeightProperty.AddOwner(
        typeof(FluentIcon),
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
    /// filled glyphs; use <see cref="Variant"/> or <see cref="Filled"/> for that purpose.
    /// </remarks>
    public FontWeight? IconWeight
    {
        get => (FontWeight?)GetValue(IconWeightProperty);
        set => SetValue(IconWeightProperty, value);
    }

    /// <summary>Identifies the <see cref="IconForeground"/> dependency property.</summary>
    /// <remarks>
    /// This is the same inheritable presentation property exposed by <see cref="IconPresenter"/>.
    /// The value remains separate from ordinary text foreground and is mapped to the native text
    /// property only by this font-backed icon implementation.
    /// </remarks>
    public static readonly DependencyProperty IconForegroundProperty =
        IconPresenter.IconForegroundProperty.AddOwner(
            typeof(FluentIcon),
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

    /// <summary>Identifies the <see cref="Variant"/> dependency property.</summary>
    /// <remarks>
    /// This is the same dependency property exposed by <see cref="IconPresenter"/>. Adding this
    /// type as an owner lets WPF's property inheritance deliver the presentation context directly;
    /// no visual-tree lookup or navigation-specific callback is involved.
    /// </remarks>
    public static readonly DependencyProperty VariantProperty = IconPresenter.VariantProperty.AddOwner(
        typeof(FluentIcon),
        new FrameworkPropertyMetadata(
            IconVariant.Default,
            FrameworkPropertyMetadataOptions.Inherits,
            propertyChangedCallback: static (d, _) =>
                // Re-coercion changes only Filled's effective value. Its source value and any application
                // binding remain intact and become effective again when the presenter returns to Default.
                d.CoerceValue(FilledProperty)
        )
    );

    /// <summary>
    /// Gets or sets the presentation variant requested for this element.
    /// </summary>
    /// <remarks>
    /// The value normally inherits from an <see cref="IconPresenter"/>. It can also be assigned
    /// directly when the icon is used outside an icon content slot.
    /// </remarks>
    public IconVariant Variant
    {
        get => (IconVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Initializes a new instance of the <see cref="FluentIcon"/> class.</summary>
    public FluentIcon()
    {
        Focusable         = false;
        TextAlignment     = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        UpdateFontFamily();
        UpdateGlyph();
    }

    /// <summary>Initializes a new instance of the <see cref="FluentIcon"/> class.</summary>
    /// <param name="symbol">The glyph to display.</param>
    public FluentIcon(SymbolRegular symbol)
        : this()
    {
        Symbol = symbol;
    }

    /// <summary>Initializes a new instance of the <see cref="FluentIcon"/> class.</summary>
    /// <param name="symbol">The glyph to display.</param>
    /// <param name="filled"><see langword="true"/> to use the filled form by default.</param>
    public FluentIcon(SymbolRegular symbol, bool filled)
        : this(symbol)
    {
        Filled = filled;
    }

    private void UpdateGlyph()
    {
        SetCurrentValue(TextProperty, Filled ? Symbol.Swap().GetString() : Symbol.GetString());
    }

    private void UpdateFontFamily()
    {
        // A dynamic resource keeps every icon synchronized with theme dictionary replacement and
        // avoids duplicating the embedded-font pack URI throughout the control implementation.
        SetResourceReference(
            FontFamilyProperty,
            Filled ? FontFamilyKeys.FluentSystemIconsFilled : FontFamilyKeys.FluentSystemIcons
        );
    }
}