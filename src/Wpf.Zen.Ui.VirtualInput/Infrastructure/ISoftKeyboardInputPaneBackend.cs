// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Abstracts the window-scoped Windows input-pane controller.
/// </summary>
internal interface ISoftKeyboardInputPaneBackend
{
    SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host);

    bool TryShow(IntPtr host);
}
