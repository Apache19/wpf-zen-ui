// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button with drop down.", FluentSymbolRegular.Filter16)]
public partial class DropDownButtonPage : INavigableView<DropDownButtonViewModel>
{
    public DropDownButtonViewModel ViewModel { get; }

    public DropDownButtonPage(DropDownButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    private void OnContextMenuCardPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.UIElement element)
        {
            element.Focus();
        }
    }

    private void OnPopupDoneClick(object sender, System.Windows.RoutedEventArgs e)
    {
        // Closing through the owner also updates any IsDropDownOpen binding.
        PopupSettingsButton.SetCurrentValue(DropDownButton.IsDropDownOpenProperty, false);
    }
}
