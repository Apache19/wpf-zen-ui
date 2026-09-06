// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes the content and actions presented by the gallery's reusable message-box dialog.
/// </summary>
/// <param name="Title">The heading displayed by the dialog.</param>
/// <param name="Message">The message displayed in the dialog body.</param>
/// <param name="PrimaryButton">The required primary action.</param>
/// <remarks>
/// This type is an immutable request rather than a binding model. A null optional action omits that
/// button. Dismissing the dialog through its top-right surface returns
/// <see cref="MessageBoxDialogResult.Dismissed"/> independently of a configured close action.
/// </remarks>
public sealed record MessageBoxDialogOptions(
    string Title,
    string Message,
    MessageBoxDialogButtonOptions PrimaryButton)
{
    /// <summary>
    /// Gets the optional dialog width limit in device-independent units.
    /// The default is null, which preserves the view's default maximum width.
    /// </summary>
    /// <remarks>
    /// Limits long messages while allowing shorter content to use less space. The view's
    /// minimum width still takes precedence, so choose a limit at least as large as that minimum.
    /// Positive infinity removes the width limit; negative values and NaN are rejected by WPF
    /// when the service creates the view.
    /// </remarks>
    public double? MaxWidth { get; init; }

    /// <summary>Gets the optional Fluent icon displayed beside the message.</summary>
    public SymbolRegular? Icon { get; init; }

    /// <summary>Gets the optional secondary action.</summary>
    public MessageBoxDialogButtonOptions? SecondaryButton { get; init; }

    /// <summary>Gets the optional close action. This action also handles the Escape key.</summary>
    public MessageBoxDialogButtonOptions? CloseButton { get; init; }

    /// <summary>
    /// Gets a value indicating whether the independent top-right dismissal surface is available.
    /// The default is true.
    /// </summary>
    public bool IsDismissible { get; init; } = true;
}
