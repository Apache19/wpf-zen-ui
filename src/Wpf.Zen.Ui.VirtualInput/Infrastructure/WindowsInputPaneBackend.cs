// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Routes window-scoped input-pane requests to the reflected COM controller.
/// </summary>
internal sealed class WindowsInputPaneBackend : ISoftKeyboardInputPaneBackend
{
    public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
    {
        return SystemInputPaneController.TryHide(host);
    }

    public bool TryShow(IntPtr host)
    {
        return SystemInputPaneController.TryShow(host);
    }
}
