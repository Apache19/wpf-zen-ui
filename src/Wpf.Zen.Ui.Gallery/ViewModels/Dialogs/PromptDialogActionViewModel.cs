// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

/// <summary>
/// Provides the binding values for one realized prompt footer action.
/// </summary>
/// <param name="Text">The text displayed by the action.</param>
/// <param name="Appearance">The visual appearance applied to the action.</param>
/// <param name="IsDefault">Whether Enter invokes the action.</param>
/// <param name="IsCancel">Whether Escape invokes the action.</param>
/// <param name="Result">The typed result returned when the action closes the dialog.</param>
public sealed record PromptDialogActionViewModel(
    string Text,
    ControlAppearance Appearance,
    bool IsDefault,
    bool IsCancel,
    PromptDialogResult Result
);
