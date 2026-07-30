// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Markup;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Markup;

/// <summary>
/// Provides a <see cref="SymbolRegular"/> value to XAML without constructing a visual element.
/// </summary>
/// <remarks>
/// Icon slots intentionally remain ordinary <see cref="object"/> content properties, so a symbol
/// name written as a bare XAML string remains text. This extension provides an explicit,
/// strongly typed way to select a <see cref="SymbolRegular"/> value while preserving that native
/// WPF content model. The returned enum value can be reused safely; each
/// <see cref="ContentPresenter"/> creates its own <see cref="SymbolIcon"/> through the implicit
/// <see cref="SymbolRegular"/> data template.
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:Button Icon="{ui:Symbol Home24}" /&gt;
/// &lt;Setter Property="Icon" Value="{ui:Symbol Settings24}" /&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Symbol))]
[MarkupExtensionReturnType(typeof(SymbolRegular))]
public sealed class SymbolExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolExtension"/> class whose
    /// <see cref="Symbol"/> has its default enum value.
    /// </summary>
    public SymbolExtension() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolExtension"/> class that returns the
    /// specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol data to provide.</param>
    public SymbolExtension(SymbolRegular symbol)
    {
        Symbol = symbol;
    }

    /// <summary>
    /// Gets or sets the symbol data returned to the XAML object writer.
    /// </summary>
    [ConstructorArgument("symbol")]
    public SymbolRegular Symbol { get; set; }

    /// <summary>
    /// Returns the configured symbol data. Visual creation is intentionally delegated to WPF's
    /// normal data-template pipeline.
    /// </summary>
    /// <param name="serviceProvider">Services supplied by the XAML object writer.</param>
    /// <returns>The configured <see cref="SymbolRegular"/> value.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) => Symbol;
}
