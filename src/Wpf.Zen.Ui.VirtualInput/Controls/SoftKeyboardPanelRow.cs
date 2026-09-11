// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Collects the visible children that share one logical soft-keyboard row.
/// </summary>
internal sealed class SoftKeyboardPanelRow(int id)
{
    internal int Id { get; } = id;

    internal List<UIElement> Children { get; } = [];
}
