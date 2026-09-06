// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes a completed local login simulation.
/// </summary>
/// <param name="UserName">
/// The demo account name. Passwords never cross the dialog service boundary.
/// </param>
public sealed record LoginDialogResult(string UserName);
