// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

/// <summary>
/// Holds the binding state for one presentation of the save-work dialog.
/// </summary>
/// <remarks>
/// The dialog service owns this instance. Initial values are copied from an immutable request and
/// the final editable selection is copied into a result, keeping presentation state out of the
/// calling page view model.
/// </remarks>
public partial class SaveWorkDialogViewModel : ObservableObject
{
    /// <summary>Initializes a new instance of the <see cref="SaveWorkDialogViewModel"/> class.</summary>
    /// <param name="options">The immutable values used to initialize this presentation.</param>
    public SaveWorkDialogViewModel(SaveWorkDialogOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Title             = options.Title;
        Message           = options.Message;
        UploadToCloudText = options.UploadToCloudText;
        UploadToCloud     = options.UploadToCloud;
    }

    /// <summary>Gets the heading displayed by the dialog.</summary>
    public string Title { get; }

    /// <summary>Gets the explanatory message displayed above the save option.</summary>
    public string Message { get; }

    /// <summary>Gets the label displayed beside the cloud-upload option.</summary>
    public string UploadToCloudText { get; }

    /// <summary>Gets or sets whether the content should also be uploaded.</summary>
    [ObservableProperty]
    public partial bool UploadToCloud { get; set; }
}
