// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Appearance;

/// <summary>
/// Specifies how the theme system should choose a concrete <see cref="ThemeVariant"/>.
/// </summary>
/// <remarks>
/// A preference is a request, not necessarily the visual result. Read
/// <see cref="Wpf.Zen.Ui.ThemeManager.EffectiveTheme"/> when code needs the resolved Light or
/// Dark variant currently used for rendering.
/// </remarks>
public enum ThemePreference
{
    /// <summary>
    /// Makes no explicit Light or Dark request. Globally, the library default palette remains effective: it is
    /// Light-compatible outside high contrast and follows the active high-contrast palette when required. On
    /// <c>Theme.Requested</c>, locally injected resources are removed so normal parent and application resource
    /// lookup resumes.
    /// </summary>
    Default,

    /// <summary>
    /// Requests the Light variant regardless of the system preference.
    /// </summary>
    Light,

    /// <summary>
    /// Requests the Dark variant regardless of the system preference.
    /// </summary>
    Dark,

    /// <summary>
    /// Automatically resolves to Light or Dark based on the current system setting.
    /// </summary>
    System,
}
