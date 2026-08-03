// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Zen.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a Fluent System Icons glyph as ordinary WPF text content.
/// </summary>
/// <remarks>
/// <para>
/// The control derives directly from <see cref="TextBlock"/> so font, foreground, data-context,
/// style, and resource inheritance retain their normal WPF behavior. It intentionally does not use
/// a private child element or a separate icon-source factory.
/// </para>
/// <para>
/// <see cref="Symbol"/> and <see cref="Filled"/> own the generated <c>Text</c>
/// and <c>FontFamily</c> values. Applications that need arbitrary text or a glyph
/// from another font should place a normal <see cref="TextBlock"/> in the icon content slot.
/// </para>
/// </remarks>
public class SymbolIcon : TextBlock
{
    #region DP: Symbol

    /// <summary>Identifies the <see cref="Symbol"/> dependency property.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(SymbolIcon),
        new FrameworkPropertyMetadata(
            SymbolRegular.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSymbolChanged
        )
    );

    /// <summary>Gets or sets the Fluent symbol displayed by this element.</summary>
    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SymbolIcon)d).UpdateGlyph();
    }

    #endregion

    #region DP: Filled

    /// <summary>Identifies the <see cref="Filled"/> dependency property.</summary>
    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(SymbolIcon),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFilledChanged,
            CoerceFilled
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the filled Fluent icon font and corresponding glyph are used.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. A containing control may temporarily coerce
    /// the effective value to <see langword="true"/> for presentation state, such as an active
    /// navigation item. Coercion does not replace this property's local value, style, or binding;
    /// the configured value becomes effective again when the presentation state ends.
    /// </remarks>
    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    private static void OnFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (SymbolIcon)d;
        self.UpdateFontFamily();
        self.UpdateGlyph();
    }

    private static object CoerceFilled(DependencyObject d, object? baseValue)
    {
        return (bool)baseValue! || (bool)d.GetValue(IsFilledRequestedProperty);
    }

    #endregion

    #region Internal DP: IsFilledRequested

    /// <summary>
    /// Identifies the internal presentation request combined with <see cref="FilledProperty"/>.
    /// </summary>
    /// <remarks>
    /// Controls such as <see cref="NavigationViewItem"/> can request a filled visual for transient
    /// state without replacing the value, binding, or style supplied to <see cref="Filled"/>. WPF
    /// coercion exposes the combined effective value and automatically restores the untouched base
    /// value when the presentation request is removed.
    /// </remarks>
    internal static readonly DependencyProperty IsFilledRequestedProperty = DependencyProperty.Register(
        "IsFilledRequested",
        typeof(bool),
        typeof(SymbolIcon),
        new PropertyMetadata(false, OnIsFilledRequestedChanged)
    );

    private static void OnIsFilledRequestedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // CoerceValue recalculates the effective value while preserving Filled's original value
        // source. In particular, an application binding is never detached by transient control state.
        d.CoerceValue(FilledProperty);
    }

    #endregion

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    public SymbolIcon()
    {
        Focusable = false;
        TextAlignment = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        UpdateFontFamily();
        UpdateGlyph();
    }

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    /// <param name="symbol">The symbol to display.</param>
    public SymbolIcon(SymbolRegular symbol)
        : this()
    {
        Symbol = symbol;
    }

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    /// <param name="symbol">The symbol to display.</param>
    /// <param name="filled">Whether to use the filled variant.</param>
    public SymbolIcon(SymbolRegular symbol, bool filled)
        : this(symbol)
    {
        Filled = filled;
    }

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    /// <param name="symbol">The symbol to display.</param>
    /// <param name="fontSize">The font size to set as a local value.</param>
    public SymbolIcon(SymbolRegular symbol, double fontSize)
        : this(symbol)
    {
        FontSize = fontSize;
    }

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    /// <param name="symbol">The symbol to display.</param>
    /// <param name="fontSize">The font size to set as a local value.</param>
    /// <param name="filled">Whether to use the filled variant.</param>
    public SymbolIcon(SymbolRegular symbol, double fontSize, bool filled)
        : this(symbol, filled)
    {
        FontSize = fontSize;
    }

    /// <summary>Initializes a new instance of the <see cref="SymbolIcon"/> class.</summary>
    /// <param name="symbol">The symbol to display.</param>
    /// <param name="filled">Whether to use the filled variant.</param>
    /// <param name="fontSize">The font size to set as a local value.</param>
    public SymbolIcon(SymbolRegular symbol, bool filled, double fontSize)
        : this(symbol, fontSize, filled) { }

    private void UpdateGlyph()
    {
        // SetCurrentValue preserves a binding on Text while still keeping Symbol authoritative when
        // the binding is absent. This mirrors WPF's own control-property update pattern.
        SetCurrentValue(TextProperty, Filled ? Symbol.Swap().GetString() : Symbol.GetString());
    }

    private void UpdateFontFamily()
    {
        // A dynamic resource is required because the font is registered by the theme dictionaries.
        // Assigning a FontFamily instance here would bypass resource replacement and duplicate the
        // pack-URI knowledge in every icon instance.
        SetResourceReference(
            FontFamilyProperty,
            Filled ? FontFamilyKeys.FluentSystemIconsFilled : FontFamilyKeys.FluentSystemIcons
        );
    }
}
