// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.StatusAndInfo;

[GalleryPage("Shows the app progress on a task.", FluentSymbolRegular.ArrowDownload24)]
public partial class ProgressBarPage : INavigableView<ProgressBarViewModel>
{
    public ProgressBarViewModel ViewModel { get; }

    public ProgressBarPage(ProgressBarViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
