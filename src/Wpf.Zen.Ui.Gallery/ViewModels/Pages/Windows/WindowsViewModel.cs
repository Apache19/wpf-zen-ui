// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Services;
using Wpf.Zen.Ui.Gallery.Views.Windows;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Windows;

public partial class WindowsViewModel(WindowsProviderService windowsProviderService) : ViewModel
{
    [ObservableProperty]
    public partial WindowCard[] WindowCards { get; set; } =
    [
        new("Editor", "Text editor with tabbed background.", SymbolRegular.ScanText24, "editor"),
#if DEBUG
        new("Sandbox", "Sandbox for controls testing.", SymbolRegular.ScanText24, "sandbox"),
#endif
    ];

    [RelayCommand]
    public void OnOpenWindow(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        switch (value)
        {
            case "editor":
                windowsProviderService.Show<EditorWindow>();
                break;

            case "sandbox":
                windowsProviderService.Show<SandboxWindow>();
                break;
        }
    }
}
