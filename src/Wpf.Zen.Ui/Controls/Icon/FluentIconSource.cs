// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes a Fluent System Icons glyph independently of the visual element that presents it.
/// </summary>
/// <remarks>
/// The immutable source can be stored in view models and resources or shared by any number of
/// controls. The library's implicit data template creates a distinct <see cref="FontIcon"/> for
/// each presentation, avoiding WPF visual-parent conflicts. The containing
/// <see cref="IconPresenter"/> may request a temporary <see cref="IconVariant"/> without mutating
/// this source.
/// The Glyph and FontFamily properties describe the source's own Filled choice. Library hosts
/// resolve the corresponding font resource in their own scope; replacing that resource affects
/// those presentations without changing this source's embedded default font.
/// </remarks>
public sealed class FluentIconSource : FontIconSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentIconSource"/> class.
    /// </summary>
    /// <param name="symbol">The glyph identity from the regular symbol catalogue.</param>
    /// <param name="filled">
    /// <see langword="true"/> to use the filled form by default; otherwise,
    /// <see langword="false"/> to use the regular form.
    /// </param>
    public FluentIconSource(FluentSymbolRegular symbol, bool filled = false)
    {
        Symbol = symbol;
        Filled = filled;
    }

    /// <summary>
    /// Gets the glyph identity shared by the regular and filled Fluent icon fonts.
    /// </summary>
    public FluentSymbolRegular Symbol { get; }

    /// <summary>
    /// Gets a value indicating whether the source prefers the filled form by default.
    /// </summary>
    public bool Filled { get; }

    /// <inheritdoc />
    public override string Glyph
    {
        get
        {
            if (Symbol == FluentSymbolRegular.Empty)
            {
                return string.Empty;
            }

            if (Filled)
            {
                // The two fonts assign different code points to the same icon name.
                string name = Symbol.ToString();
                if (!Enum.TryParse(name, out FluentSymbolFilled symbol))
                {
                    return string.Empty;
                }

                return char.ConvertFromUtf32((int)symbol);
            }

            return char.ConvertFromUtf32((int)Symbol);
        }
    }

    /// <inheritdoc />
    public override FontFamily FontFamily
    {
        get => Filled ? DefaultFonts.Filled : DefaultFonts.Regular;
    }

    /// <inheritdoc />
    public override object FontFamilyResourceKey
    {
        get => Filled ? FontFamilyKeys.FluentSystemIconsFilled : FontFamilyKeys.FluentSystemIcons;
    }

    /// <inheritdoc />
    public override FontIconSource WithVariant(IconVariant variant)
    {
        bool filled = variant switch
        {
            IconVariant.Regular => false,
            IconVariant.Filled  => true,
            _                   => Filled,
        };
        if (filled == Filled)
        {
            return this;
        }

        return new FluentIconSource(Symbol, filled);
    }

    /// <summary>
    /// Loads embedded defaults once from the theme's font dictionary. Only font data is retained;
    /// no application resources or host state become part of a shared source.
    /// </summary>
    private static class DefaultFonts
    {
        internal static readonly FontFamily Regular;
        internal static readonly FontFamily Filled;

        static DefaultFonts()
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Resources/Fonts.xaml"),
            };

            Regular = (FontFamily)resources[FontFamilyKeys.FluentSystemIcons];
            Filled  = (FontFamily)resources[FontFamilyKeys.FluentSystemIconsFilled];
        }
    }
}