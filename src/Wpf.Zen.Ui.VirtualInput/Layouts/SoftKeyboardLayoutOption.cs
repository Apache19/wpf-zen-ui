// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Describes one implemented supplemental layout available in the current input environment.
/// </summary>
/// <param name="Id">Stable selection identifier.</param>
/// <param name="DisplayName">Presentation text, replaceable by a settings provider.</param>
public sealed record SoftKeyboardLayoutOption(string Id, string DisplayName);
