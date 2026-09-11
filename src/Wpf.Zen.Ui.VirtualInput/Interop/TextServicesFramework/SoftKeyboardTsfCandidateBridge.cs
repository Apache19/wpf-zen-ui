// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Routes keyboard instances to the candidate session owned by their WPF dispatcher.
/// </summary>
/// <remarks>
/// WPF owns a single Text Services Framework thread manager per dispatcher. Sharing the session keeps
/// COM sinks, UI-less activation, and candidate presentation aligned with that same lifetime rather
/// than allowing individual controls to compete for process-global TSF state.
/// </remarks>
internal static class SoftKeyboardTsfCandidateBridge
{
    private static readonly ConditionalWeakTable<Dispatcher, TsfCandidateSession> Sessions = new();

    internal static void Register(SoftKeyboard keyboard)
    {
        var session = Sessions.GetValue(
            keyboard.Dispatcher,
            static dispatcher => new TsfCandidateSession(dispatcher)
        );

        session.Register(keyboard);
    }

    internal static void Unregister(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Unregister(keyboard);
        }
    }

    internal static void Refresh(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Refresh(keyboard);
        }
    }

    internal static void ReleasePresentation(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.ReleasePresentation(keyboard);
        }
    }

    internal static bool TryCommit(SoftKeyboard keyboard, uint index)
    {
        return Sessions.TryGetValue(keyboard.Dispatcher, out var session)
            && session.TryCommit(keyboard, index);
    }

    internal static bool TryMovePage(SoftKeyboard keyboard, int delta)
    {
        return Sessions.TryGetValue(keyboard.Dispatcher, out var session)
            && session.TryMovePage(keyboard, delta);
    }
}