// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.Controls.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Creates optional unpositioned physical-letter key material for compact layouts.
/// </summary>
/// <remarks>
/// Consumers choose their own row placement, commands and geometry. This factory neither
/// selects an input scope nor owns an address, language or page layout.
/// </remarks>
internal static class PhysicalKeyboardKeys
{
    private static readonly Key[][] LetterRows =
    [
        [Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P],
        [Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L],
        [Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M],
    ];

    private static readonly IReadOnlyDictionary<Key, string[]> Alternates =
        new Dictionary<Key, string[]>
        {
            [Key.A] = ["á", "à", "â", "ä", "ã", "å", "æ"],
            [Key.C] = ["ç"],
            [Key.E] = ["é", "è", "ê", "ë"],
            [Key.I] = ["í", "ì", "î", "ï"],
            [Key.N] = ["ñ"],
            [Key.O] = ["ó", "ò", "ô", "ö", "õ", "ø", "œ"],
            [Key.S] = ["ß", "š"],
            [Key.U] = ["ú", "ù", "û", "ü"],
            [Key.Y] = ["ý", "ÿ"],
            [Key.Z] = ["ž"],
        };

    internal static IReadOnlyList<IReadOnlyList<SoftKeyboardKey>> CreateRows(SoftKeyboardLayoutContext context)
    {
        var shifted = context.ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;
        var rows    = new List<IReadOnlyList<SoftKeyboardKey>>();

        foreach (var keyRow in LetterRows)
        {
            var keys = keyRow.Select(key =>
            {
                var label = KeyboardLayoutLabels.GetLabel(key, shifted, context.KeyboardLayoutHandle);

                IEnumerable<string>? alternates = null;

                // Alternates belong to the displayed character, not the physical VK position.
                // For example, AZERTY moves A onto the Q virtual-key position.
                if (Enum.TryParse<Key>(label.ToUpperInvariant(), out var labeledKey)
                    && Alternates.TryGetValue(labeledKey, out var variants))
                {
                    alternates = shifted
                        ? variants.Select(static value => value.ToUpperInvariant())
                        : variants;
                }

                return new SoftKeyboardKey(
                    kind:           SoftKeyboardKeyKind.VirtualKey,
                    content:        label,
                    automationName: label,
                    key:            key,
                    alternates:     alternates
                );
            });

            rows.Add([.. keys]);
        }

        return rows;
    }
}
