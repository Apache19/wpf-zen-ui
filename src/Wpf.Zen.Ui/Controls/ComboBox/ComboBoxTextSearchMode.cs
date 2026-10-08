// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies matching rules for an editable <see cref="ComboBox"/>.
/// </summary>
/// <remarks>
/// Custom modes use ComboBox.SearchInteraction for presentation and confirmation timing.
/// Prefix retains WPF's native completion while typing.
/// </remarks>
public enum ComboBoxTextSearchMode
{
    /// <summary>
    /// Uses WPF's native prefix search and inline completion. This is the default.
    /// </summary>
    Prefix,

    /// <summary>
    /// Matches the entire input anywhere in a candidate's text.
    /// </summary>
    Contains,

    /// <summary>
    /// Splits input on whitespace and requires every token to occur in the candidate.
    /// Tokens may occur in any order.
    /// </summary>
    Tokens,

    /// <summary>
    /// Matches whitespace-separated tokens, allowing gaps between characters within each token.
    /// For example, "cbi" matches "ComboBoxItem". Tokens may occur in any order.
    /// </summary>
    Fuzzy,
}
