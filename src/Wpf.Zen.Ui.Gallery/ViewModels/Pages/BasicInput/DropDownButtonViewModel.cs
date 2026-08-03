// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class DropDownButtonViewModel : ViewModel
{
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready.";

    [RelayCommand]
    private void OnCut()
    {
        StatusMessage = "Cut command executed.";
    }

    [RelayCommand]
    private void OnCopy()
    {
        StatusMessage = "Copy command executed.";
    }

    [RelayCommand]
    private void OnPaste()
    {
        StatusMessage = "Paste command executed.";
    }

    [RelayCommand]
    private void OnDelete()
    {
        StatusMessage = "Delete command executed.";
    }

    [RelayCommand]
    private void OnSelectAll()
    {
        StatusMessage = "Select All command executed.";
    }

    [RelayCommand]
    private void OnUndo()
    {
        StatusMessage = "Undo command executed.";
    }

    [RelayCommand]
    private void OnRedo()
    {
        StatusMessage = "Redo command executed.";
    }

    [RelayCommand]
    private void OnOpen()
    {
        StatusMessage = "Open command executed.";
    }

    [RelayCommand]
    private void OnSave()
    {
        StatusMessage = "Save command executed.";
    }

    [RelayCommand]
    private void OnSaveAs()
    {
        StatusMessage = "Save As command executed.";
    }

    [RelayCommand]
    private void OnNew()
    {
        StatusMessage = "New command executed.";
    }

    [RelayCommand]
    private void OnSettings()
    {
        StatusMessage = "Settings command executed.";
    }

    [RelayCommand]
    private void OnRefresh()
    {
        StatusMessage = "Refresh command executed.";
    }

    [RelayCommand]
    private void OnPrint()
    {
        StatusMessage = "Print command executed.";
    }

    [RelayCommand]
    private void OnExit()
    {
        StatusMessage = "Exit command executed.";
    }
}
