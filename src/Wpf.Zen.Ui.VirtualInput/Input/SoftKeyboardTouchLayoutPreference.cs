// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// A snapshot of ITfFnGetPreferredTouchKeyboardLayout; it contains no key geometry or input actions.
/// </summary>
/// <param name="Type">The native layout family.</param>
/// <param name="Id">The native layout identifier, interpreted together with the family and active profile.</param>
public sealed record SoftKeyboardTouchLayoutPreference(SoftKeyboardTouchLayoutType Type, ushort Id);
