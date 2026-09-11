// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Abstracts process-global Windows input injection and foreground-window validation.
/// </summary>
internal interface ISoftKeyboardInputBackend
{
    bool SendVirtualKey(Key key, bool shift);

    bool SendUnicode(string text);

    bool IsHostForeground(IntPtr host);
}
