// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Shares stateless key presentation data among built-in surfaces.
/// </summary>
internal static class KeyboardLayoutParts
{
    // Two logical columns form the nominal width of an ordinary character key. The half-key
    // granularity makes physical-row staggering and 1.5-key command caps exactly representable.
    internal const int DefaultColumnCount = 20;

    // Icon sources are immutable presentation data. Sharing them avoids rebuilding identical
    // descriptors whenever Shift or the requested surface regenerates the layout; the implicit
    // FluentIconSource data template still creates an independent visual for every key button.
    internal static readonly FluentIconSource ShiftIcon        = new(SymbolRegular.KeyboardShift24);
    internal static readonly FluentIconSource ShiftOnceIcon    = new(SymbolRegular.KeyboardShiftUppercase24);
    internal static readonly FluentIconSource ShiftLockedIcon  = new(SymbolRegular.KeyboardShiftUppercase24, filled: true);
    internal static readonly FluentIconSource BackspaceIcon    = new(SymbolRegular.Backspace24);
    internal static readonly FluentIconSource InputMethodsIcon = new(SymbolRegular.Globe24);
    internal static readonly FluentIconSource MoveLeftIcon     = new(SymbolRegular.ArrowLeft24);
    internal static readonly FluentIconSource MoveRightIcon    = new(SymbolRegular.ArrowRight24);
    internal static readonly FluentIconSource PrevSymbolsIcon  = new(SymbolRegular.CaretLeft24, filled: true);
    internal static readonly FluentIconSource MoreSymbolsIcon  = new(SymbolRegular.CaretRight24, filled: true);
    internal static readonly FluentIconSource SpaceIcon        = new(SymbolRegular.Spacebar24);
    internal static readonly FluentIconSource EnterIcon        = new(SymbolRegular.ArrowEnterLeft24);

    /// <summary>
    /// Creates an IME toggle whose cap reflects the observed input state and active input language.
    /// Direct or unknown states display A; native input uses a language-specific label.
    /// </summary>
    internal static SoftKeyboardKey CreateImeKey(SoftKeyboardLayoutContext context)
    {
        return new SoftKeyboardKey(
            kind: SoftKeyboardKeyKind.ToggleIme,
            content: context.ImeState == InputMethodState.On ? GetNativeInputLabel(context.Culture) : "A",
            automationName: context.ImeState == InputMethodState.On
                ? "Native input; switch to direct input"
                : "Direct input; enable input method"
        );
    }

    /// <summary>
    /// Selects a compact native-script label for recognized input languages.
    /// Unmapped languages and explicitly Latin-script cultures use their uppercase ISO language
    /// code. An invariant or undetermined language displays IME.
    /// </summary>
    /// <remarks>
    /// These caps identify the input language, not the service's current conversion mode.
    /// Languages sharing a writing system can share a cap. Regional variants use the same label.
    /// </remarks>
    private static string GetNativeInputLabel(CultureInfo culture)
    {
        // Use the layout snapshot so application UI culture cannot change the input-mode label.
        var language = culture.TwoLetterISOLanguageName;

        // Honor an explicit Latin-script variant, such as hi-Latn, before selecting native letters.
        foreach (var subtag in culture.Name.Split('-'))
        {
            if (string.Equals(subtag, "Latn", StringComparison.OrdinalIgnoreCase))
            {
                return language.ToUpperInvariant();
            }
        }

        return language switch
        {
            // East Asian input-mode labels.
            "zh" => "中",
            "ja" => "あ",
            "ko" => "한",

            // Representative letters from South and Southeast Asian writing systems.
            "hi" or "mr" or "ne" or "sa" => "अ",
            "bn" or "as" => "অ",
            "gu" => "અ",
            "or" => "ଅ",
            "ta" => "அ",
            "te" => "అ",
            "kn" => "ಅ",
            "ml" => "അ",
            "si" => "අ",
            "th" => "ก",
            "lo" => "ກ",
            "km" => "ក",
            "my" => "က",
            "bo" or "dz" => "ཀ",

            // West Asian, African, and European native-script labels.
            "ar" => "ع",
            "fa" => "ف",
            "ur" => "ا",
            "ps" => "پ",
            "he" or "yi" => "א",
            "am" or "ti" => "አ",
            "hy" => "Ա",
            "ka" => "ა",
            "el" => "α",
            "ru" or "uk" or "bg" or "be" or "mk" => "Ж",

            "" or "iv" or "und" => "IME",
            _ => language.ToUpperInvariant(),
        };
    }

    /// <summary>
    /// Selects a key-cap description that distinguishes inactive, one-shot, and locked Shift
    /// without placing stateful visual elements in the immutable layout model.
    /// </summary>
    internal static FluentIconSource GetShiftContent(SoftKeyboardShiftState shiftState)
    {
        return shiftState switch
        {
            SoftKeyboardShiftState.Off    => ShiftIcon,
            SoftKeyboardShiftState.Once   => ShiftOnceIcon,
            SoftKeyboardShiftState.Locked => ShiftLockedIcon,
            _                             => throw new ArgumentOutOfRangeException(nameof(shiftState)),
        };
    }

    internal static SoftKeyboardKey CreateTextKey(string value)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, value, value, text: value);
    }
}
