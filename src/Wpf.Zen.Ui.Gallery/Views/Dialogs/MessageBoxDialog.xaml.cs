// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

namespace Wpf.Zen.Ui.Gallery.Views.Dialogs;

/// <summary>
/// Presents a reusable, data-driven message box within the current
/// <see cref="ContentDialogHost"/> scope.
/// </summary>
/// <remarks>
/// The action template closes the dialog through <see cref="ContentDialog.CloseCommand"/> and
/// returns the role assigned by <see cref="MessageBoxDialogViewModel"/>. No view instance or WPF
/// command crosses the dialog-service boundary.
/// </remarks>
public partial class MessageBoxDialog : ContentDialog
{
    /// <summary>Initializes a new instance of the <see cref="MessageBoxDialog"/> class.</summary>
    public MessageBoxDialog()
    {
        InitializeComponent();
    }
}
