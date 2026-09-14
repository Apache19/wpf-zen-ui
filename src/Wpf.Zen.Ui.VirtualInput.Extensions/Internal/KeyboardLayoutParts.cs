// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Internal;

/// <summary>
/// Shares stateless key presentation resources among extension-provided keyboard layouts.
/// </summary>
internal static class KeyboardLayoutParts
{
    // Icon sources are immutable presentation data. Sharing them keeps extension layouts aligned
    // with the built-in keyboard and avoids allocating identical descriptors for every rebuild.
    // The FluentIconSource data template still creates an independent visual for every key button.
    internal static readonly FluentIconSource InputMethodsIcon = new(SymbolRegular.Globe24);
    internal static readonly FluentIconSource SpaceIcon        = new(SymbolRegular.Spacebar24);
    internal static readonly FluentIconSource MoveLeftIcon     = new(SymbolRegular.ArrowLeft24);
    internal static readonly FluentIconSource MoveDownIcon     = new(SymbolRegular.ArrowDown24);
    internal static readonly FluentIconSource BackspaceIcon    = new(SymbolRegular.Backspace24);
    internal static readonly FluentIconSource EnterIcon        = new(SymbolRegular.ArrowEnterLeft24);
}
