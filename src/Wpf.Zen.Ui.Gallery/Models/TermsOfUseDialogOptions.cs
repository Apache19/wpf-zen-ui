// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>Describes the immutable content supplied to a terms-of-use dialog.</summary>
/// <param name="TermsText">The terms presented to the user.</param>
public sealed record TermsOfUseDialogOptions(string TermsText)
{
    /// <summary>
    /// Gets the optional dialog width limit in device-independent units.
    /// The default is null, which preserves the view's default maximum width.
    /// </summary>
    /// <remarks>
    /// Limits long text while allowing shorter content to use less space. The view's
    /// minimum width still takes precedence, so choose a limit at least as large as that minimum.
    /// Positive infinity removes the width limit; negative values and NaN are rejected by WPF
    /// when the service creates the view.
    /// </remarks>
    public double? MaxWidth { get; init; }

    /// <summary>Gets the heading displayed by the dialog.</summary>
    public string Title { get; init; } = "Terms of use";

    /// <summary>Gets the label displayed beside the acceptance check box.</summary>
    public string AgreementText { get; init; } = "I agree to these terms";

    /// <summary>Gets the validation message shown after an unsuccessful acceptance attempt.</summary>
    public string ValidationMessage { get; init; } = "You must accept these terms of use to continue.";

    /// <summary>Gets the text displayed by the acceptance action.</summary>
    public string AcceptButtonText { get; init; } = "Accept";
}
