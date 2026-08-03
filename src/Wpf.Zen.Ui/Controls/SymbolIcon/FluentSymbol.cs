// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;
using Wpf.Zen.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a Fluent System Icon symbol that can be displayed inside a text element.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;TextBlock&gt;
///     &lt;ui:FluentSymbol Symbol="Document24" Filled="True" FontSize="12" /&gt;
///     &lt;Run Text="{Binding DeviceName}" /&gt;
/// &lt;/TextBlock&gt;
/// </code>
/// </example>
public class FluentSymbol : Run
{
    static FluentSymbol()
    {
        BaselineAlignmentProperty.OverrideMetadata(
            typeof(FluentSymbol),
            new FrameworkPropertyMetadata(BaselineAlignment.Center)
        );
    }

    /// <summary>Identifies the <see cref="Symbol"/> dependency property.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(FluentSymbol),
        new PropertyMetadata(SymbolRegular.Empty, OnSymbolChanged)
    );

    /// <summary>Identifies the <see cref="Filled"/> dependency property.</summary>
    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(FluentSymbol),
        new PropertyMetadata(false, OnFilledChanged)
    );

    /// <summary>
    /// Gets or sets the displayed <see cref="SymbolRegular"/>.
    /// </summary>
    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the filled variant of the icon is displayed.
    /// </summary>
    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentSymbol"/> class.
    /// </summary>
    public FluentSymbol()
    {
        UpdateFontFamily();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentSymbol"/> class.
    /// </summary>
    public FluentSymbol(SymbolRegular symbol, bool filled = false)
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
        ((FluentSymbol)d).UpdateGlyph();
    }

    private static void OnFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (FluentSymbol)d;
        self.UpdateFontFamily();
        self.UpdateGlyph();
    }
}
