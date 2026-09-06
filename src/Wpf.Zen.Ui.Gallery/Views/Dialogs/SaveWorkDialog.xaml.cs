// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

namespace Wpf.Zen.Ui.Gallery.Views.Dialogs;

/// <summary>
/// Presents the save-work choices and binds editable state supplied by
/// <see cref="SaveWorkDialogViewModel"/>.
/// </summary>
public partial class SaveWorkDialog : ContentDialog
{
    /// <summary>Initializes a new instance of the <see cref="SaveWorkDialog"/> class.</summary>
    public SaveWorkDialog()
    {
        InitializeComponent();
    }
}
