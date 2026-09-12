// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Interop;

/// <summary>
/// Resolves the active Windows kana mapping and configures native hiragana input through IMM's TSF bridge.
/// </summary>
internal static class JapaneseImeInput
{
    private static readonly ConcurrentDictionary<IntPtr, Dictionary<char, (Key Key, bool Shift)>> KanaMaps = new();

    internal static (Key Key, bool Shift)[] GetKanaKeys(string kana, IntPtr layout)
    {
        var map  = KanaMaps.GetOrAdd(layout, BuildKanaMap);
        var keys = new List<(Key, bool)>();

        foreach (var character in kana.Normalize(NormalizationForm.FormD))
        {
            if (!map.TryGetValue(character, out var key))
            {
                return [];
            }

            keys.Add(key);
        }

        return [.. keys];
    }

    private static Dictionary<char, (Key Key, bool Shift)> BuildKanaMap(IntPtr layout)
    {
        // Windows' own kana translation table distinguishes 101/106-key and customized layouts.
        // Hard-coded OEM keys would type ろ instead of dakuten on a 101-key layout. Probe with a
        // private keyboard-state buffer and ToUnicodeEx's no-state-change flag; cache by borrowed HKL.
        var map = new Dictionary<char, (Key, bool)>();

        foreach (var shifted in new[] { false, true })
        {
            for (var virtualKey = 0x20; virtualKey <= 0xFE; virtualKey++)
            {
                // Numpad arithmetic keys can translate to kana-width punctuation while bypassing
                // the IME selector. Only probe the printable main keyboard, never keypad commands.
                if (virtualKey is not (>= 0x30 and <= 0x5A)
                    and not (>= 0xBA and <= 0xC0)
                    and not (>= 0xDB and <= 0xDF)
                    and not 0xE2)
                {
                    continue;
                }

                var key = KeyInterop.KeyFromVirtualKey(virtualKey);
                if (key == Key.None)
                {
                    continue;
                }

                var label = KeyboardLayoutLabels
                    .GetLabel(key, shifted, layout, kana: true)
                    .Normalize(NormalizationForm.FormKC)
                    .Normalize(NormalizationForm.FormD);

                if (label.Length != 1)
                {
                    continue;
                }

                var character = label[0];

                if (character is >= '\u30a1' and <= '\u30f6')
                {
                    character = (char)(character - 0x60);
                }

                if (character is not (>= '\u3041' and <= '\u309a') and not '\u30fc')
                {
                    continue;
                }

                if (!map.ContainsKey(character))
                {
                    map.Add(character, (key, shifted));
                }
            }
        }

        return map;
    }

    internal static bool TryEnableKanaMode(IntPtr window)
    {
        // These panels explicitly represent full-width hiragana, not the alphabetic keyboard.
        // WPF's conversion setter merges existing flags and cannot clear Roman. IMM applies the
        // exact language/shape/key-mode bits while preserving unrelated conversion and sentence
        // preferences. The acquired HIMC belongs to this HWND and is always released on this thread.
        var context = NativeMethods.ImmGetContext(window);

        if (context == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!NativeMethods.ImmGetConversionStatus(context, out var conversion, out var sentence))
            {
                return false;
            }

            const uint native = 0x0001, katakana = 0x0002, fullShape = 0x0008, roman = 0x0010;

            var kanaMode = (conversion | native | fullShape) & ~(katakana | roman);

            return NativeMethods.ImmSetConversionStatus(context, kanaMode, sentence);
        }
        finally
        {
            NativeMethods.ImmReleaseContext(window, context);
        }
    }

    // todo: 使用 CsWin32
    private static class NativeMethods
    {
        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmSetConversionStatus(IntPtr context, uint conversion, uint sentence);

        [DllImport("imm32.dll")]
        internal static extern IntPtr ImmGetContext(IntPtr window);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmGetConversionStatus(IntPtr context, out uint conversion, out uint sentence);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
    }
}