// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies character mapping for the complete Fluent font catalogues.
/// </summary>
public class FluentIconSourceTests
{
    /// <summary>
    /// Each named icon must resolve to the code point in its selected font, including
    /// supplementary Unicode characters that require a surrogate pair.
    /// </summary>
    [Fact]
    public void EverySymbol_ResolvesRegularAndFilledCodePoints()
    {
        var names = Enum.GetNames(typeof(FluentSymbolRegular));
        var filledNames = Enum.GetNames(typeof(FluentSymbolFilled));
        Assert.Equal(names.Length, filledNames.Length);

        foreach (string name in names)
        {
            var regular = (FluentSymbolRegular)Enum.Parse(typeof(FluentSymbolRegular), name);
            var filled = (FluentSymbolFilled)Enum.Parse(typeof(FluentSymbolFilled), name);
            var regularSource = new FluentIconSource(regular);
            var filledSource = new FluentIconSource(regular, filled: true);
            string expectedRegular = regular == FluentSymbolRegular.Empty
                ? string.Empty
                : char.ConvertFromUtf32((int)regular);
            string expectedFilled = filled == FluentSymbolFilled.Empty
                ? string.Empty
                : char.ConvertFromUtf32((int)filled);

            Assert.Equal(expectedRegular, regularSource.Glyph);
            Assert.Equal(expectedFilled, filledSource.Glyph);
        }
    }
}
