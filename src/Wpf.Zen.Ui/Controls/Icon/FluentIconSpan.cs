// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;
using Wpf.Zen.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a Fluent System Icons glyph inline inside a WPF text element.
/// </summary>
/// <remarks>
/// The run is the inline counterpart of <see cref="FluentIcon"/>. It participates in the same
/// <see cref="IconPresenter.VariantProperty"/> contract, including when the value inherits through
/// a containing <see cref="System.Windows.Controls.TextBlock"/> into the text content tree.
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;TextBlock&gt;
///     &lt;ui:FluentIconSpan Symbol="Document24" Filled="True" FontSize="12" /&gt;
///     &lt;Run Text="{Binding DeviceName}" /&gt;
/// &lt;/TextBlock&gt;
/// </code>
/// </example>
public class FluentIconSpan : Run
{
    static FluentIconSpan()
    {
        BaselineAlignmentProperty.OverrideMetadata(
            typeof(FluentIconSpan),
            new FrameworkPropertyMetadata(BaselineAlignment.Center)
        );
    }

    /// <summary>Identifies the <see cref="Symbol"/> dependency property.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(FluentIconSpan),
        new PropertyMetadata(SymbolRegular.Empty, OnSymbolChanged)
    );

    /// <summary>
    /// Gets or sets the displayed <see cref="SymbolRegular"/>.
    /// </summary>
    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Identifies the <see cref="Filled"/> dependency property.</summary>
    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(FluentIconSpan),
        new FrameworkPropertyMetadata(false, OnFilledChanged, CoerceFilled)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the filled variant of the icon is displayed.
    /// </summary>
    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    /// <summary>Identifies the <see cref="Variant"/> dependency property.</summary>
    public static readonly DependencyProperty VariantProperty = IconPresenter.VariantProperty.AddOwner(
        typeof(FluentIconSpan),
        new FrameworkPropertyMetadata(
            IconVariant.Default,
            FrameworkPropertyMetadataOptions.Inherits,
            OnVariantChanged
        )
    );

    /// <summary>Gets or sets the presentation variant requested for this inline icon.</summary>
    public IconVariant Variant
    {
        get => (IconVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentIconSpan"/> class.
    /// </summary>
    public FluentIconSpan()
    {
        UpdateFontFamily();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentIconSpan"/> class.
    /// </summary>
    public FluentIconSpan(SymbolRegular symbol, bool filled = false)
        : this()
    {
        Symbol = symbol;
        Filled = filled;
    }

    private void UpdateGlyph()
    {
        SetCurrentValue(TextProperty, Filled ? Symbol.Swap().GetString() : Symbol.GetString());
    }

    private void UpdateFontFamily()
    {
        SetResourceReference(
            FontFamilyProperty,
            Filled ? FontFamilyKeys.FluentSystemIconsFilled : FontFamilyKeys.FluentSystemIcons
        );
    }

    private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FluentIconSpan)d).UpdateGlyph();
    }

    private static void OnFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (FluentIconSpan)d;
        self.UpdateFontFamily();
        self.UpdateGlyph();
    }

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(FilledProperty);
    }

    private static object CoerceFilled(DependencyObject d, object? baseValue)
    {
        return ((FluentIconSpan)d).Variant switch
        {
            IconVariant.Regular => false,
            IconVariant.Filled => true,
            _ => (bool)baseValue!,
        };
    }
}
