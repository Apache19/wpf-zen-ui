// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Concurrent;
using System.Text;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Resolves Windows kana mappings and controls Japanese conversion mode through generated interop.
/// </summary>
internal static class JapaneseImeInput
{
    private static readonly ConcurrentDictionary<IntPtr, Dictionary<char, (Key Key, bool Shift)>> KanaMaps = new();

    internal static (Key Key, bool Shift)[] GetKanaKeys(string kana, IntPtr layout)
    {
        var map = KanaMaps.GetOrAdd(layout, BuildKanaMap);
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

    internal static unsafe bool TryEnableKanaMode(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var context = PInvoke.ImmGetContext((HWND)window);
        if (context == default)
        {
            return false;
        }

        try
        {
            IME_CONVERSION_MODE conversion;
            IME_SENTENCE_MODE sentence;
            if (!PInvoke.ImmGetConversionStatus(context, &conversion, &sentence))
            {
                return false;
            }

            const IME_CONVERSION_MODE Native = (IME_CONVERSION_MODE)0x0001;
            const IME_CONVERSION_MODE Katakana = (IME_CONVERSION_MODE)0x0002;
            const IME_CONVERSION_MODE FullShape = (IME_CONVERSION_MODE)0x0008;
            const IME_CONVERSION_MODE Roman = (IME_CONVERSION_MODE)0x0010;
            var kanaMode = (conversion | Native | FullShape) & ~(Katakana | Roman);
            return PInvoke.ImmSetConversionStatus(context, kanaMode, sentence);
        }
        finally
        {
            _ = PInvoke.ImmReleaseContext((HWND)window, context);
        }
    }

    private static Dictionary<char, (Key Key, bool Shift)> BuildKanaMap(IntPtr layoutHandle)
    {
        var map = new Dictionary<char, (Key, bool)>();

        foreach (var shifted in new[] { false, true })
        {
            for (var virtualKey = 0x20; virtualKey <= 0xFE; virtualKey++)
            {
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

                var label = GetKanaLabel(key, shifted, layoutHandle)
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

    private static unsafe string GetKanaLabel(Key key, bool shifted, IntPtr layoutHandle)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        HKL layout = new(layoutHandle);
        byte* state = stackalloc byte[256];
        new Span<byte>(state, 256).Clear();
        state[0x15] = 1;
        if (shifted)
        {
            state[0x10] = 0x80;
        }

        const int BufferLength = 8;
        char* buffer = stackalloc char[BufferLength];
        var scanCode = PInvoke.MapVirtualKeyEx(
            (uint)virtualKey,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC,
            layout
        );
        var length = PInvoke.ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            state,
            buffer,
            BufferLength,
            4,
            layout
        );

        return length == 1 ? buffer[0].ToString() : string.Empty;
    }
}
