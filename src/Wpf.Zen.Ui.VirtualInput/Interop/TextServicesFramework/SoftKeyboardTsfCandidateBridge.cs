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
/// WPF owns a single Text Services Framework thread manager per dispatcher. Sharing the session
/// keeps
/// COM sinks and candidate presentation aligned with that same lifetime. Activation remains owned
/// by WPF so native-only text services can initialize regardless of page navigation order.
/// </remarks>
internal static class SoftKeyboardTsfCandidateBridge
{
    private static readonly ConditionalWeakTable<Dispatcher, TsfCandidateSession> Sessions = new();

    /// <summary>
    /// Attaches a source host to the candidate session shared by this keyboard's dispatcher.
    /// </summary>
    internal static void Register(SoftKeyboard keyboard, SoftKeyboardCandidateHost host)
    {
        var session = Sessions.GetValue(
            keyboard.Dispatcher,
            static dispatcher => new TsfCandidateSession(dispatcher)
        );

        session.Register(keyboard, host);
    }

    /// <summary>
    /// Removes the keyboard registration; the session releases sinks when its last consumer leaves.
    /// </summary>
    internal static void Unregister(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Unregister(keyboard);
        }
    }

    /// <summary>
    /// Asks an existing dispatcher session to reconcile focused-document candidate presentation.
    /// </summary>
    internal static void Refresh(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Refresh(keyboard);
        }
    }

    /// <summary>
    /// Returns this keyboard's embedded surface to native presentation within its shared session.
    /// </summary>
    internal static void ReleasePresentation(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.ReleasePresentation(keyboard);
        }
    }

    /// <summary>
    /// Forwards submission to the owning dispatcher session, or declines when none is registered.
    /// </summary>
    internal static Task<SoftKeyboardCandidateOperationResult> TryCommitAsync(
        SoftKeyboard keyboard,
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
        }

        return session.TryCommitAsync(keyboard, candidate, cancellationToken);
    }

    /// <summary>
    /// Forwards page navigation to the owning session so native element lifetime checks stay
    /// shared.
    /// </summary>
    internal static Task<SoftKeyboardCandidateOperationResult> TryMovePageAsync(
        SoftKeyboard keyboard,
        int delta,
        CancellationToken cancellationToken)
    {
        if (!Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
        }

        return session.TryMovePageAsync(keyboard, delta, cancellationToken);
    }
}
