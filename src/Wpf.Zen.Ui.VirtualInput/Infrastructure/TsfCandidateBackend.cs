// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Routes candidate lifecycle operations to the dispatcher-scoped TSF bridge.
/// </summary>
internal sealed class TsfCandidateBackend : ISoftKeyboardCandidateBackend
{
    public void Register(SoftKeyboard keyboard)
    {
        SoftKeyboardTsfCandidateBridge.Register(keyboard);
    }

    public void Unregister(SoftKeyboard keyboard)
    {
        SoftKeyboardTsfCandidateBridge.Unregister(keyboard);
    }

    public void Refresh(SoftKeyboard keyboard)
    {
        SoftKeyboardTsfCandidateBridge.Refresh(keyboard);
    }

    public void ReleasePresentation(SoftKeyboard keyboard)
    {
        SoftKeyboardTsfCandidateBridge.ReleasePresentation(keyboard);
    }

    public bool TryMovePage(SoftKeyboard keyboard, int delta)
    {
        return SoftKeyboardTsfCandidateBridge.TryMovePage(keyboard, delta);
    }

    public bool TryCommit(SoftKeyboard keyboard, uint index)
    {
        return SoftKeyboardTsfCandidateBridge.TryCommit(keyboard, index);
    }
}
