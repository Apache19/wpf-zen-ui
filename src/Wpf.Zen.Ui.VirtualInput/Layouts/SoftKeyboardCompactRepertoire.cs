// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Describes punctuation and symbols for the core compact keyboard renderer.
/// </summary>
public sealed class SoftKeyboardCompactRepertoire
{
    /// <summary>
    /// Gets or initializes the comma shown on the letter and symbol command rows.
    /// </summary>
    public string Comma { get; init; } = ",";

    /// <summary>
    /// Gets or initializes the period shown on the letter and symbol command rows.
    /// </summary>
    public string Period { get; init; } = ".";

    /// <summary>
    /// Gets or initializes the trailing character on the first symbol page.
    /// </summary>
    public string SymbolsTail { get; init; } = "?";

    /// <summary>
    /// Gets or initializes the trailing character on the second symbol page.
    /// </summary>
    public string MoreSymbolsTail { get; init; } = "\\";

    /// <summary>
    /// Gets or initializes an optional additional key placed after the second letter row.
    /// </summary>
    public SoftKeyboardKey? ExtraLetterKey { get; init; }

    /// <summary>
    /// Gets or initializes the ten characters on the second row of the first symbol page.
    /// </summary>
    public IReadOnlyList<string> SymbolsSecond { get; init; }
        = ["!", "@", "#", "$", "%", "&", "_", "-", "=", "+"];

    /// <summary>
    /// Gets or initializes the seven characters on the third row of the first symbol page.
    /// </summary>
    public IReadOnlyList<string> SymbolsThird { get; init; }
        = [";", ":", "(", ")", "/", "'", "\""];

    /// <summary>
    /// Gets or initializes the ten characters on the second row of the second symbol page.
    /// </summary>
    public IReadOnlyList<string> MoreSymbolsSecond { get; init; }
        = ["%", "[", "]", "{", "}", "<", ">", "€", "£", "¥"];

    /// <summary>
    /// Gets or initializes the seven characters on the third row of the second symbol page.
    /// </summary>
    public IReadOnlyList<string> MoreSymbolsThird { get; init; }
        = ["*", "`", "°", "×", "÷", "~", "|"];
}
