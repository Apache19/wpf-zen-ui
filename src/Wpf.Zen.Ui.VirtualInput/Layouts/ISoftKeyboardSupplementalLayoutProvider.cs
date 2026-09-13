// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Supplies selectable supplemental layouts for the current input environment.
/// </summary>
public interface ISoftKeyboardSupplementalLayoutProvider
{
    /// <summary>
    /// Creates immutable registrations that are currently available.
    /// </summary>
    /// <param name="context">The current layout context.</param>
    /// <returns>Registrations with stable, provider-wide identifiers.</returns>
    IEnumerable<SoftKeyboardSupplementalLayout> GetLayouts(SoftKeyboardLayoutContext context);
}
