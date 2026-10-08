// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Controls when custom editable search updates selection and how candidates are displayed.
/// Native prefix completion is unaffected by this setting.
/// </summary>
public enum ComboBoxSearchInteraction
{
    /// <summary>
    /// Matches and fills the best result only on Enter or TryCommitTextSearch. This is the default.
    /// </summary>
    ExplicitCommit,

    /// <summary>
    /// Opens the full list and selects the best match while preserving the typed query.
    /// Enter, an item click, or TryCommitTextSearch fills the selected result's complete text.
    /// </summary>
    AutoSelect,

    /// <summary>
    /// Opens a list of matching text labels. Navigation is provisional until Enter, a candidate
    /// click, or TryCommitTextSearch commits it. The original selection survives editing.
    /// </summary>
    Filter,
}
