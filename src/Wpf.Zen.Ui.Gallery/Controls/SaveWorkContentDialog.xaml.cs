// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Controls;

/// <summary>XAML presentation of save options and business choices.</summary>
/// <remarks>The application dialog service supplies a SaveWorkDialogData instance as DataContext.</remarks>
public partial class SaveWorkContentDialog : ContentDialog
{
    /// <summary>Initializes a new instance of the <see cref="SaveWorkContentDialog"/> class from XAML.</summary>
    public SaveWorkContentDialog()
    {
        InitializeComponent();
    }
}
