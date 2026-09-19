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
}

/// <summary>
/// Separates a platform mode command from backends that expose interpreted mode state.
/// </summary>
internal interface ISoftKeyboardInputModeButtonBackend
{
    SoftKeyboardInputModeButton? GetInputModeButton(SoftKeyboardInputMethod inputMethod);

    SoftKeyboardInputModeCommandResult TryInvokeInputModeButton(
        SoftKeyboardInputMethod inputMethod, Rect screenBounds);
}

/// <summary>
/// Supplies optional settled state samples independently of profile discovery and commands.
/// </summary>
internal interface ISoftKeyboardInputStateBackend
{
    /// <summary>
    /// Reads both values through TSF, or through IMM for the supplied focused editor HWND.
    /// Missing contexts and failed reads remain explicitly unavailable.
    /// </summary>
    SoftKeyboardInputObservation ReadState(bool useImm, IntPtr window);
}
