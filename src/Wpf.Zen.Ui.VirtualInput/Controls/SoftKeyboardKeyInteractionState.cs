// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Tracks the mutually exclusive pointer interaction owned by a key-cap instance.
/// </summary>
internal enum SoftKeyboardKeyInteractionState
{
    Idle,
    Pressed,
    Repeating,
    Alternatives,
}
