// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Abstracts the lifecycle and commands of an embedded TSF candidate presentation.
/// </summary>
internal interface ISoftKeyboardCandidateBackend
{
    void Register(SoftKeyboard keyboard);

    void Unregister(SoftKeyboard keyboard);

    void Refresh(SoftKeyboard keyboard);

    void ReleasePresentation(SoftKeyboard keyboard);

    bool TryMovePage(SoftKeyboard keyboard, int delta);

    bool TryCommit(SoftKeyboard keyboard, uint index);
}
