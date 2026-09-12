// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Stable identifiers for supplemental letter surfaces, independent of number/symbol page navigation.
/// </summary>
public static class SoftKeyboardSupplementalLayouts
{
    /// <summary>
    /// Follow the input method's recognized preference; otherwise use the standard letter surface.
    /// </summary>
    public const string Automatic = "Automatic";

    /// <summary>
    /// The ordinary alphabetic keyboard.
    /// </summary>
    public const string Standard = "Standard";

    /// <summary>
    /// The Japanese fifty-sound grid.
    /// </summary>
    public const string JapaneseFifty = "JapaneseFifty";

    /// <summary>
    /// The Japanese twelve-key kana groups.
    /// </summary>
    public const string JapaneseTwelve = "JapaneseTwelve";

    /// <summary>
    /// The Taiwan standard phonetic keyboard.
    /// </summary>
    public const string Zhuyin = "Zhuyin";
}
