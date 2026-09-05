// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.Controls;

/// <summary>Presents agreement data and focuses the input when validation fails.</summary>
public partial class TermsOfUseContentDialog : ContentDialog
{
    /// <summary>Initializes a new instance of the <see cref="TermsOfUseContentDialog"/> class from XAML.</summary>
    public TermsOfUseContentDialog()
    {
        InitializeComponent();
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TermsOfUseDialogData data)
        {
            return;
        }

        if (data.TryAccept())
        {
            Close(true);
            return;
        }

        // Focus is a view concern; validation state is supplied by the bound data.
        _ = CheckBox.Focus();
        CheckBox.BringIntoView();
    }
}
