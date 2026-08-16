// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Win32;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Visualizes a semantic navigation graph as an interactive application map.", SymbolRegular.Map24)]
public partial class NavigationMapPage : INavigableView<NavigationMapViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="NavigationMapPage"/> class.</summary>
    /// <param name="viewModel">The view model resolved from the active navigation scope.</param>
    public NavigationMapPage(NavigationMapViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    /// <summary>Gets the view model used by this sample page.</summary>
    public NavigationMapViewModel ViewModel { get; }

    private void OnSaveLayoutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension    = true,
            DefaultExt      = ".navigationmap.json",
            Filter          = "Navigation map layout (*.navigationmap.json)|*.navigationmap.json|JSON files (*.json)|*.json",
            FileName        = "gallery.navigationmap.json",
            OverwritePrompt = true,
            Title           = "Save navigation map layout",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _ = ExecuteFileOperation(
            () => ViewModel.SaveLayout(dialog.FileName),
            $"Saved {ViewModel.Layout.Placements.Count} placements to {dialog.FileName}."
        );
    }

    private void OnLoadLayoutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            DefaultExt      = ".navigationmap.json",
            Filter          = "Navigation map layout (*.navigationmap.json)|*.navigationmap.json|JSON files (*.json)|*.json",
            Multiselect     = false,
            Title           = "Load navigation map layout",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (
            !ExecuteFileOperation(
                () => ViewModel.LoadLayout(dialog.FileName),
                $"Loaded layout from {dialog.FileName}."
            )
        )
        {
            return;
        }

        GalleryNavigationMap.ClearLayoutHistory();
        GalleryNavigationMap.SetCurrentValue(
            NavigationMapView.LayoutModeProperty,
            NavigationMapLayoutMode.Manual
        );
    }

    private bool ExecuteFileOperation(Action operation, string successMessage)
    {
        try
        {
            operation();
            EditorStatus.SetCurrentValue(TextBlock.TextProperty, successMessage);
            return true;
        }
        catch (Exception exception)
        {
            // The gallery keeps the editor visible after malformed external data. The view model
            // validates into a temporary collection, so a failed load never destroys current work.
            EditorStatus.SetCurrentValue(
                TextBlock.TextProperty,
                $"Layout operation failed: {exception.Message}"
            );
            return false;
        }
    }
}
