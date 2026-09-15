// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Abstracts enumeration, activation, and state changes for Windows input methods.
/// </summary>
internal interface ISoftKeyboardInputMethodBackend
{
    void Refresh(ObservableCollection<SoftKeyboardInputMethod> destination, out SoftKeyboardInputMethod? active);

    bool TryActivate(SoftKeyboardInputMethod profile);

    SoftKeyboardInputMode? GetInputMode(SoftKeyboardInputMethod inputMethod);

    bool TrySwitchInputMode(SoftKeyboardInputMethod inputMethod, SoftKeyboardInputMode currentMode);
}
