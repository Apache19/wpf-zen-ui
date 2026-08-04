// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
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
    /// <summary>Identifies the <see cref="Symbol"/> dependency property.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(FluentIcon),
        new FrameworkPropertyMetadata(
            SymbolRegular.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSymbolChanged
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
            false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFilledChanged,
            CoerceFilled
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
            OnVariantChanged
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
        Focusable = false;
        TextAlignment = TextAlignment.Center;
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

    private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FluentIcon)d).UpdateGlyph();
    }

    private static void OnFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (FluentIcon)d;
        self.UpdateFontFamily();
        self.UpdateGlyph();
    }

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Re-coercion changes only Filled's effective value. Its source value and any application
        // binding remain intact and become effective again when the presenter returns to Default.
        d.CoerceValue(FilledProperty);
    }

    private static object CoerceFilled(DependencyObject d, object? baseValue)
    {
        return ((FluentIcon)d).Variant switch
        {
            IconVariant.Regular => false,
            IconVariant.Filled => true,
            _ => (bool)baseValue!,
        };
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
