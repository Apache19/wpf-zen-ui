// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Appearance;

/// <summary>
/// Identifies the concrete visual variant currently produced by the theme system.
/// </summary>
/// <remarks>
/// Unlike <see cref="ThemePreference"/>, a variant is never a request or a resolution policy. It is
/// always the final Light or Dark result after System and high-contrast behavior have
/// been resolved. Keeping those states out of this enum makes it safe to use for rendering and
/// platform integration without adding defensive fallback branches.
/// </remarks>
public enum ThemeVariant
{
    /// <summary>
    /// The effective palette uses light surfaces and dark foreground content.
    /// </summary>
    Light,

    /// <summary>
    /// The effective palette uses dark surfaces and light foreground content.
    /// </summary>
    Dark,
}
