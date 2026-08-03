// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Win32;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.OpSystem;

public partial class FilePickerViewModel : ViewModel
{
    [ObservableProperty]
    public partial Visibility OpenedFilePathVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string OpenedFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility OpenedPicturePathVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string OpenedPicturePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility OpenedMultiplePathVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string OpenedMultiplePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility OpenedFolderPathVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string OpenedFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileToSaveName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileToSaveContents { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility SavedFileNoticeVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string SavedFileNotice { get; set; } = string.Empty;

    [RelayCommand]
    public void OnOpenFile()
    {
        OpenedFilePathVisibility = Visibility.Collapsed;

        OpenFileDialog openFileDialog = new()
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "All files (*.*)|*.*",
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        if (!File.Exists(openFileDialog.FileName))
        {
            return;
        }

        OpenedFilePath = openFileDialog.FileName;
        OpenedFilePathVisibility = Visibility.Visible;
    }

    [RelayCommand]
    public void OnOpenPicture()
    {
        OpenedPicturePathVisibility = Visibility.Collapsed;

        OpenFileDialog openFileDialog = new()
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Filter = "Image files (*.bmp;*.jpg;*.jpeg;*.png)|*.bmp;*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        if (!File.Exists(openFileDialog.FileName))
        {
            return;
        }

        OpenedPicturePath = openFileDialog.FileName;
        OpenedPicturePathVisibility = Visibility.Visible;
    }

    [RelayCommand]
    public void OnOpenMultiple()
    {
        OpenedMultiplePathVisibility = Visibility.Collapsed;

        OpenFileDialog openFileDialog = new()
        {
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "All files (*.*)|*.*",
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        if (openFileDialog.FileNames.Length == 0)
        {
            return;
        }

        var fileNames = openFileDialog.FileNames;

        OpenedMultiplePath = string.Join("\n", fileNames);
        OpenedMultiplePathVisibility = Visibility.Visible;
    }

    [RelayCommand]
    public void OnOpenFolder()
    {
#if NET8_0_OR_GREATER
        OpenedFolderPathVisibility = Visibility.Collapsed;

        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (openFolderDialog.ShowDialog() != true)
        {
            return;
        }

        if (openFolderDialog.FolderNames.Length == 0)
        {
            return;
        }

        OpenedFolderPath = string.Join("\n", openFolderDialog.FolderNames);
        OpenedFolderPathVisibility = Visibility.Visible;
#else
        OpenedFolderPath = "OpenFolderDialog requires .NET 8 or newer";
        OpenedFolderPathVisibility = Visibility.Visible;
#endif
    }

    [RelayCommand]
    public async Task OnSaveFile(CancellationToken cancellation)
    {
        SavedFileNoticeVisibility = Visibility.Collapsed;

        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Text Files (*.txt)|*.txt",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (!string.IsNullOrEmpty(FileToSaveName))
        {
            var invalidChars =
                new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            saveFileDialog.FileName = string.Join(
                    "_",
                    FileToSaveName.Split(invalidChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                )
                .Trim();
        }

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        if (File.Exists(saveFileDialog.FileName))
        {
            // Protect the user from accidental writes
            return;
        }

        try
        {
#if NET10_0_OR_GREATER
            await File.WriteAllTextAsync(saveFileDialog.FileName, FileToSaveContents, cancellation);
#else
            // .NET Framework: File.WriteAllTextAsync doesn't exist. Use synchronous API.
            cancellation.ThrowIfCancellationRequested();
            File.WriteAllText(saveFileDialog.FileName, FileToSaveContents);
#endif
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);

            return;
        }

        SavedFileNoticeVisibility = Visibility.Visible;
        SavedFileNotice = $"File {saveFileDialog.FileName} was saved.";
    }
}
