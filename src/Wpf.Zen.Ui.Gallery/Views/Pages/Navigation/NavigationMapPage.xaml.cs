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
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    /// <summary>Gets the view model used by this sample page.</summary>
    public NavigationMapViewModel ViewModel { get; }

    private void OnPreviousSearchResultClick(object sender, RoutedEventArgs e)
    {
        _ = GalleryNavigationMap.FindPrevious();
    }

    private void OnNextSearchResultClick(object sender, RoutedEventArgs e)
    {
        _ = GalleryNavigationMap.FindNext();
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => GalleryNavigationMap.ZoomOut();

    private void OnZoomToFitClick(object sender, RoutedEventArgs e) =>
        _ = GalleryNavigationMap.ZoomToFit();

    private void OnZoomInClick(object sender, RoutedEventArgs e) => GalleryNavigationMap.ZoomIn();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => GalleryNavigationMap.SelectAll();

    private void OnAlignToGridClick(object sender, RoutedEventArgs e)
    {
        bool hasSelection = GalleryNavigationMap.SelectedNodeKeys.Count > 0;
        bool aligned = GalleryNavigationMap.AlignToGrid();
        EditorStatus.SetCurrentValue(
            TextBlock.TextProperty,
            aligned
                ? hasSelection ? "Aligned the selection to the grid." : "Aligned every node to the grid."
                : "Enable Manual editing before aligning nodes."
        );
    }

    private void OnAlignLeftClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(GalleryNavigationMap.AlignSelection(NavigationMapAlignment.Left));

    private void OnAlignHorizontalCenterClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(
            GalleryNavigationMap.AlignSelection(NavigationMapAlignment.HorizontalCenter)
        );

    private void OnAlignRightClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(GalleryNavigationMap.AlignSelection(NavigationMapAlignment.Right));

    private void OnAlignTopClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(GalleryNavigationMap.AlignSelection(NavigationMapAlignment.Top));

    private void OnAlignVerticalCenterClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(
            GalleryNavigationMap.AlignSelection(NavigationMapAlignment.VerticalCenter)
        );

    private void OnAlignBottomClick(object sender, RoutedEventArgs e) =>
        ReportAlignmentResult(GalleryNavigationMap.AlignSelection(NavigationMapAlignment.Bottom));

    private void OnUndoClick(object sender, RoutedEventArgs e) => _ = GalleryNavigationMap.Undo();

    private void OnRedoClick(object sender, RoutedEventArgs e) => _ = GalleryNavigationMap.Redo();

    private void OnResetLayoutClick(object sender, RoutedEventArgs e)
    {
        GalleryNavigationMap.ResetLayout();
        EditorStatus.SetCurrentValue(
            TextBlock.TextProperty,
            "Layout reset to the grid-aligned hierarchical starting arrangement."
        );
    }

    private void OnSaveLayoutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".navigationmap.json",
            Filter = "Navigation map layout (*.navigationmap.json)|*.navigationmap.json|JSON files (*.json)|*.json",
            FileName = "gallery.navigationmap.json",
            OverwritePrompt = true,
            Title = "Save navigation map layout",
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
            DefaultExt = ".navigationmap.json",
            Filter = "Navigation map layout (*.navigationmap.json)|*.navigationmap.json|JSON files (*.json)|*.json",
            Multiselect = false,
            Title = "Load navigation map layout",
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
        GalleryNavigationMap.SetCurrentValue(NavigationMapView.IsEditingProperty, true);
    }

    private void ReportAlignmentResult(bool aligned)
    {
        EditorStatus.SetCurrentValue(
            TextBlock.TextProperty,
            aligned
                ? "Selection aligned to the primary node."
                : "Select at least two nodes in Manual edit mode before aligning."
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