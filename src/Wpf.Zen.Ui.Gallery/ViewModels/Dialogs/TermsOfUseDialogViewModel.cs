// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

/// <summary>
/// Holds agreement and validation state for one presentation of the terms-of-use dialog.
/// </summary>
public partial class TermsOfUseDialogViewModel : ObservableObject
{
    /// <summary>Initializes a new instance of the <see cref="TermsOfUseDialogViewModel"/> class.</summary>
    /// <param name="options">The immutable content used by this presentation.</param>
    public TermsOfUseDialogViewModel(TermsOfUseDialogOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Title             = options.Title;
        TermsText         = options.TermsText;
        AgreementText     = options.AgreementText;
        ValidationMessage = options.ValidationMessage;
        AcceptButtonText  = options.AcceptButtonText;
    }

    /// <summary>Gets the heading displayed by the dialog.</summary>
    public string Title { get; }

    /// <summary>Gets the terms presented to the user.</summary>
    public string TermsText { get; }

    /// <summary>Gets the label displayed beside the acceptance check box.</summary>
    public string AgreementText { get; }

    /// <summary>Gets the message shown when acceptance validation fails.</summary>
    public string ValidationMessage { get; }

    /// <summary>Gets the text displayed by the acceptance action.</summary>
    public string AcceptButtonText { get; }

    /// <summary>Gets or sets whether the user agrees to the terms.</summary>
    [ObservableProperty]
    public partial bool IsAccepted { get; set; }

    /// <summary>Gets whether an unsuccessful acceptance attempt should display an error.</summary>
    [ObservableProperty]
    public partial bool HasValidationError { get; private set; }

    /// <summary>Validates acceptance and updates the error state bound by the view.</summary>
    /// <returns><see langword="true"/> when the agreement can be accepted.</returns>
    public bool TryAccept()
    {
        HasValidationError = !IsAccepted;
        return IsAccepted;
    }
}
