// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates reusable <see cref="FluentSystemIcon"/> data for a WPF content slot.
/// </summary>
/// <remarks>
/// The extension returns presentation-independent data rather than a visual element. WPF's
/// implicit data-template pipeline creates a distinct <see cref="SymbolIcon"/> wherever the
/// value is presented, so the same value can safely be used by styles and multiple controls.
/// The family-specific name is intentional: future font-icon families can define their own
/// models and markup extensions without overloading the ambiguous term <c>Symbol</c>.
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:Button Icon="{ui:FluentSystemIcon Home24}" /&gt;
/// &lt;Setter Property="Icon" Value="{ui:FluentSystemIcon Settings24, Filled=True}" /&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Symbol))]
[MarkupExtensionReturnType(typeof(FluentSystemIcon))]
public sealed class FluentSystemIconExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentSystemIconExtension"/> class whose
    /// symbol has the default enum value and whose form is regular.
    /// </summary>
    public FluentSystemIconExtension() { }

    /// <summary>Initializes a new instance of the <see cref="FluentSystemIconExtension"/> class.</summary>
    /// <param name="symbol">The glyph identity to provide.</param>
    public FluentSystemIconExtension(SymbolRegular symbol)
    {
        Symbol = symbol;
    }

    /// <summary>Initializes a new instance of the <see cref="FluentSystemIconExtension"/> class.</summary>
    /// <param name="symbol">The glyph identity to provide.</param>
    /// <param name="filled"><see langword="true"/> to request its filled form.</param>
    public FluentSystemIconExtension(SymbolRegular symbol, bool filled)
        : this(symbol)
    {
        Filled = filled;
    }

    /// <summary>Gets or sets the glyph identity returned in the icon model.</summary>
    [ConstructorArgument("symbol")]
    public SymbolRegular Symbol { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the returned icon model requests the filled form.
    /// </summary>
    [ConstructorArgument("filled")]
    public bool Filled { get; set; }

    /// <summary>Creates the immutable icon data consumed by the default data template.</summary>
    /// <param name="serviceProvider">Services supplied by the XAML object writer.</param>
    /// <returns>A reusable description containing both the symbol and its requested form.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new FluentSystemIcon(Symbol, Filled);
}
