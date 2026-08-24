// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a reusable <see cref="FluentIconSource"/> for an icon content property.
/// </summary>
/// <remarks>
/// The extension returns data, not a visual element. Consequently the value is valid in style
/// setters and may be presented more than once. The family-specific extension name also keeps
/// future icon systems independent: each can introduce its own source and implicit data template.
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:Button Icon="{ui:FluentIconSource Home24}" /&gt;
/// &lt;Setter Property="Icon" Value="{ui:FluentIconSource Settings24, Filled=True}" /&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Symbol))]
[MarkupExtensionReturnType(typeof(FluentIconSource))]
public sealed class FluentIconSourceExtension : MarkupExtension
{
    /// <summary>Initializes a new instance of the <see cref="FluentIconSourceExtension"/> class.</summary>
    public FluentIconSourceExtension() { }

    /// <summary>Initializes a new instance of the <see cref="FluentIconSourceExtension"/> class.</summary>
    /// <param name="symbol">The Fluent glyph identity to provide.</param>
    public FluentIconSourceExtension(SymbolRegular symbol)
    {
        Symbol = symbol;
    }

    /// <summary>Initializes a new instance of the <see cref="FluentIconSourceExtension"/> class.</summary>
    /// <param name="symbol">The Fluent glyph identity to provide.</param>
    /// <param name="filled"><see langword="true"/> to prefer the filled form.</param>
    public FluentIconSourceExtension(SymbolRegular symbol, bool filled)
        : this(symbol)
    {
        Filled = filled;
    }

    /// <summary>Gets or sets the Fluent glyph identity returned by the source.</summary>
    [ConstructorArgument("symbol")]
    public SymbolRegular Symbol { get; set; }

    /// <summary>Gets or sets a value indicating whether the source prefers the filled form by default.</summary>
    [ConstructorArgument("filled")]
    public bool Filled { get; set; }

    /// <summary>Creates the immutable source consumed by the Fluent icon data template.</summary>
    /// <param name="serviceProvider">Services supplied by the XAML object writer.</param>
    /// <returns>A reusable Fluent icon source.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new FluentIconSource(Symbol, Filled);
}
