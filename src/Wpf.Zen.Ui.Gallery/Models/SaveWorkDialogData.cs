// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>Editable data for one save confirmation; contains no view or template references.</summary>
public partial class SaveWorkDialogData : ObservableObject
{
    /// <summary>Gets or sets whether the content should also be uploaded. Defaults to false.</summary>
    [ObservableProperty]
    public partial bool UploadToCloud { get; set; }
}
