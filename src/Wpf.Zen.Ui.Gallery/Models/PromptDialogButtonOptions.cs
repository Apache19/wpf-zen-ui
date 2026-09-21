// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes the presentation of one generic prompt action.
/// </summary>
/// <param name="Text">
/// The text displayed by the action.
/// </param>
public sealed record PromptDialogButtonOptions(string Text)
{
    /// <summary>
    /// Gets the visual appearance applied to the action.
    /// </summary>
    public ControlAppearance Appearance { get; init; } = ControlAppearance.Normal;

    /// <summary>
    /// Gets a value indicating whether Enter invokes the action through WPF's default-button
    /// behavior.
    /// </summary>
    /// <remarks>
    /// Destructive actions should normally leave this value false so an unqualified Enter key does
    /// not confirm an irreversible operation.
    /// </remarks>
    public bool IsDefault { get; init; }
}
