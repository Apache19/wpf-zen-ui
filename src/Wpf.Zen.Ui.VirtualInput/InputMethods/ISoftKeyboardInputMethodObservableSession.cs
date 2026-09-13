// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Adds change invalidation to an input-method session whose state can change externally.
/// </summary>
/// <remarks>
/// Raise <see cref="StateInvalidated"/> on the owning WPF Dispatcher. The event is a signal to
/// perform a new read; it does not imply that a multi-stage native transition has completed.
/// Sessions without this interface are observed by the soft keyboard's recovery timer.
/// </remarks>
public interface ISoftKeyboardInputMethodObservableSession : ISoftKeyboardInputMethodSession
{
    /// <summary>
    /// Occurs when the catalog, active profile, or focused-document input mode may have changed.
    /// </summary>
    event EventHandler StateInvalidated;
}