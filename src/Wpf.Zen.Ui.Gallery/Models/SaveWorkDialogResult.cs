// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Represents the completed save-work decision and the option selected when it was made.
/// </summary>
/// <param name="Choice">The business action selected by the user.</param>
/// <param name="UploadToCloud">Whether cloud upload was selected when the dialog closed.</param>
public sealed record SaveWorkDialogResult(SaveWorkChoice Choice, bool UploadToCloud);
