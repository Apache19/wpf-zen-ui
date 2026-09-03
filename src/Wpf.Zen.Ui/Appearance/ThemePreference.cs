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
/// Dark variant currently used for rendering. The library uses <see cref="Light"/> as its initial
/// preference so an explicitly initialized process-wide theme agrees with Generic.xaml's fallback palette.
/// </remarks>
public enum ThemePreference
{
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
