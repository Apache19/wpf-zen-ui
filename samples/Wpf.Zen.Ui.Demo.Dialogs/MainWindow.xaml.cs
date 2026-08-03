// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Demo.Dialogs;

public partial class MainWindow
{
    public MainWindow()
    {
        // Initialize WPF window
        InitializeComponent();
    }

    private async void OnShowDialogClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Dispatch to the UI queue
            _ = await ShowSampleDialogAsync();
        }
        catch
        {
            // ignored
        }
    }

    private Task<ContentDialogResult> ShowSampleDialogAsync()
    {
        // Defining dialog object
        ContentDialog myDialog = new()
        {
            Title               = "My sample dialog",
            Content             = "Content of the dialog",
            CloseButtonText     = "Close button",
            PrimaryButtonText   = "Primary button",
            SecondaryButtonText = "Secondary button",
            // Setting the dialog container
            DialogHost          = ContentDialogHost
        };

        // Showing the dialog
        return myDialog.ShowAsync(CancellationToken.None);
    }
}
