// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Settings;

/// <summary>
/// Produces a settings tree from the current input environment without creating UI or mutating the keyboard.
/// </summary>
/// <remarks>
/// Implementations can compose the default provider's nodes, replace their presentation with record
/// copies, and add application policies. Call SoftKeyboard.RefreshSettings after external policy data
/// changes. Provider exceptions are exposed by SettingsError and fall back to the built-in settings.
/// </remarks>
public interface ISoftKeyboardSettingsProvider
{
    /// <summary>
    /// Creates a fresh settings tree for the supplied snapshot.
    /// </summary>
    /// <param name="context">Current settings and input environment; never null.</param>
    /// <returns>Root nodes in menu order. The keyboard materializes the sequence immediately.</returns>
    IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context);
}
