// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>Names the built-in keyboard pages. Custom providers may use any other nonempty, case-sensitive identifier.</summary>
/// <remarks>Use an application-qualified name for custom pages. Page navigation is owned by the keyboard,
/// so custom pages receive the same composition cancellation and target-reset behavior as built-in pages.</remarks>
public static class SoftKeyboardPages
{
    /// <summary>Selects the initial page from the editor input scope.</summary>
    public const string Automatic = "Automatic";

    /// <summary>Identifies the alphabetic page.</summary>
    public const string Letters = "Letters";

    /// <summary>Identifies the numeric page.</summary>
    public const string Numbers = "Numbers";

    /// <summary>Identifies the first symbol page.</summary>
    public const string Symbols = "Symbols";

    /// <summary>Identifies the second symbol page.</summary>
    public const string MoreSymbols = "MoreSymbols";

    /// <summary>Gets the immutable built-in catalog; this is not a restriction on custom identifiers.</summary>
    public static IReadOnlyList<string> BuiltInPages { get; } =
        Array.AsReadOnly([Automatic, Letters, Numbers, Symbols, MoreSymbols]);
}
