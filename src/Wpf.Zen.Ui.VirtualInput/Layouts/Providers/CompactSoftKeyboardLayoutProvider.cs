// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Builds compact letter and symbol pages from application- or extension-owned literal data.
/// </summary>
public sealed class CompactSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly CompactKeyboardRepertoire _repertoire;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSoftKeyboardLayoutProvider"/> class.
    /// </summary>
    /// <param name="repertoire">The punctuation, symbols, and optional extra letter key.</param>
    public CompactSoftKeyboardLayoutProvider(SoftKeyboardCompactRepertoire repertoire)
    {
        if (repertoire is null)
        {
            throw new ArgumentNullException(nameof(repertoire));
        }

        _repertoire = new CompactKeyboardRepertoire
        {
            Comma = ValidateText(repertoire.Comma, nameof(repertoire.Comma)),
            Period = ValidateText(repertoire.Period, nameof(repertoire.Period)),
            SymbolsTail = ValidateText(repertoire.SymbolsTail, nameof(repertoire.SymbolsTail)),
            MoreSymbolsTail = ValidateText(
                repertoire.MoreSymbolsTail,
                nameof(repertoire.MoreSymbolsTail)
            ),
            ExtraLetterKey = repertoire.ExtraLetterKey,
            SymbolsSecond = ValidateRow(repertoire.SymbolsSecond, 10, nameof(repertoire.SymbolsSecond)),
            SymbolsThird = ValidateRow(repertoire.SymbolsThird, 7, nameof(repertoire.SymbolsThird)),
            MoreSymbolsSecond = ValidateRow(
                repertoire.MoreSymbolsSecond,
                10,
                nameof(repertoire.MoreSymbolsSecond)
            ),
            MoreSymbolsThird = ValidateRow(
                repertoire.MoreSymbolsThird,
                7,
                nameof(repertoire.MoreSymbolsThird)
            ),
        };
    }

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Page switch
        {
            SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters =>
                LetterKeyboardLayout.Create(context, _repertoire),
            SoftKeyboardPages.Symbols or SoftKeyboardPages.MoreSymbols =>
                SymbolKeyboardLayout.Create(context, _repertoire),
            _ => null,
        };
    }

    private static string ValidateText(string value, string parameterName)
    {
        return !string.IsNullOrEmpty(value)
            ? value
            : throw new ArgumentException("A compact layout value cannot be empty.", parameterName);
    }

    private static string[] ValidateRow(
        IReadOnlyList<string> values,
        int requiredCount,
        string parameterName)
    {
        if (values is null || values.Count != requiredCount || values.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException(
                $"The compact layout row must contain {requiredCount} nonempty values.",
                parameterName
            );
        }

        return [.. values];
    }
}
