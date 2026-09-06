// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

namespace Wpf.Zen.Ui.Gallery.Views.Dialogs;

/// <summary>
/// Presents terms and retains focus in the agreement area when acceptance validation fails.
/// </summary>
public partial class TermsOfUseDialog : ContentDialog
{
    /// <summary>Initializes a new instance of the <see cref="TermsOfUseDialog"/> class.</summary>
    public TermsOfUseDialog()
    {
        InitializeComponent();
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TermsOfUseDialogViewModel viewModel)
        {
            return;
        }

        if (viewModel.TryAccept())
        {
            Close(true);
            return;
        }

        // Focus is deliberately retained by the view: validation state belongs to the view model,
        // while choosing a concrete focus target depends on this XAML template's visual structure.
        _ = AgreementCheckBox.Focus();
    }
}
