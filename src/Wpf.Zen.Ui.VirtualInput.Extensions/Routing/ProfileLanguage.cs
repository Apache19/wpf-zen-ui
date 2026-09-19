// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Routing;

/// <summary>
/// Routes language capabilities using the active profile, or culture when no profile is known.
/// Matching a language does not establish support for a service's compartment values.
/// </summary>
internal static class ProfileLanguage
{
    /// <summary>
    /// Matches all Chinese profile sublanguages, falling back to the Chinese culture code.
    /// </summary>
    internal static bool IsChinese(SoftKeyboardLayoutContext context)
    {
        return Matches(context, 0x04, "zh");
    }

    /// <summary>
    /// Matches Japanese profiles, falling back to the Japanese culture code.
    /// </summary>
    internal static bool IsJapanese(SoftKeyboardLayoutContext context)
    {
        return Matches(context, 0x11, "ja");
    }

    /// <summary>
    /// Uses the primary language identifier before considering the ambient culture.
    /// </summary>
    private static bool Matches(SoftKeyboardLayoutContext context, int languageId, string culture)
    {
        return context.InputMethod is { } inputMethod
            ? (inputMethod.LanguageId & 0x03FF) == languageId
            : context.Culture.TwoLetterISOLanguageName == culture;
    }
}
