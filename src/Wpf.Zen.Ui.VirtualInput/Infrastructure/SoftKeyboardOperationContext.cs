// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Carries one compound operation's execution and input ownership through provider callbacks.
/// </summary>
/// <remarks>
/// Owner validation runs on the keyboard dispatcher. A publication queued during execution may
/// arrive after normal completion, but is still rejected after cancellation or newer work.
/// Snapshot changes are acknowledgements, so they are validated separately before input steps.
/// </remarks>
internal sealed class SoftKeyboardOperationContext(
    SoftKeyboardOperationSlot.Operation execution, Func<bool> isOwnerCurrent,
    Func<bool>? isInputCurrent = null)
{
    /// <summary>
    /// Gets whether execution is still active; safe to inspect before queueing from a worker.
    /// </summary>
    internal bool IsExecuting => execution.IsCurrent;

    /// <summary>
    /// Gets whether callbacks, input and completion still belong to the original owner.
    /// </summary>
    internal bool IsCurrent => IsExecuting && CanPublish;

    /// <summary>
    /// Gets whether input can still use the captured request snapshot, before acknowledgement.
    /// </summary>
    internal bool CanSendInput => IsCurrent && (isInputCurrent is null || isInputCurrent());

    /// <summary>
    /// Gets whether a previously queued publication still belongs to the latest operation.
    /// </summary>
    internal bool CanPublish => execution.IsLatest && isOwnerCurrent();
}
