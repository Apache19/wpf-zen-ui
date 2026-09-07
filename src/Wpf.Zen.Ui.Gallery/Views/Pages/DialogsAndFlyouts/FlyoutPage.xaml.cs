// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Contextual popup.", SymbolRegular.AppTitle24)]
public partial class FlyoutPage : INavigableView<FlyoutViewModel>
{
    public FlyoutViewModel ViewModel { get; }

    public FlyoutPage(FlyoutViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    private void OnDestinationClick(object sender, RoutedEventArgs e)
    {
        var option = (System.Windows.Controls.Button)sender;
        DestinationTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, (string)option.Content);
        FlyoutService.HideAttachedFlyout(DestinationTextBox);
        DestinationTextBox.CaretIndex = DestinationTextBox.Text.Length;
    }
}