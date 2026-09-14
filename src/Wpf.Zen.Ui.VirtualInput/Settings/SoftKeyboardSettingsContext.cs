// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.Settings;

/// <summary>
/// A read-only settings-policy input, refreshed when the menu opens or keyboard state changes.
/// </summary>
public sealed class SoftKeyboardSettingsContext
{
    internal SoftKeyboardSettingsContext(
        SoftKeyboardLayoutContext layout,
        SoftKeyboardAutoShowMode autoShowMode,
        SoftKeyboardAutoHideMode autoHideMode,
        IEnumerable<SoftKeyboardLayoutOption> layouts)
    {
        Layout           = layout;
        AutoShowMode     = autoShowMode;
        AutoHideMode     = autoHideMode;
        AvailableLayouts = [with([.. layouts])];
    }

    /// <summary>
    /// Gets the active profile, observed IME state, page, input scope and supplemental selection.
    /// </summary>
    public SoftKeyboardLayoutContext Layout { get; }

    /// <summary>
    /// Gets the current automatic opening policy.
    /// </summary>
    public SoftKeyboardAutoShowMode AutoShowMode { get; }

    /// <summary>
    /// Gets the current automatic closing policy.
    /// </summary>
    public SoftKeyboardAutoHideMode AutoHideMode { get; }

    /// <summary>
    /// Gets implemented and recognized layouts, including the automatic selection.
    /// </summary>
    public ReadOnlyCollection<SoftKeyboardLayoutOption> AvailableLayouts { get; }
}