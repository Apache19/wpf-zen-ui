// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Routes a selected long-press value through a cancellable custom input action.
/// </summary>
public interface IAsyncSoftKeyboardAlternateKeyAction : IAsyncSoftKeyboardKeyAction
{
    /// <summary>
    /// Executes a selected alternative through the guarded action context.
    /// </summary>
    /// <param name="context">Services that expire when the task completes or loses ownership.</param>
    /// <param name="alternative">A value from the invoked key's alternatives.</param>
    /// <param name="cancellationToken">Cancels work after a newer input operation takes ownership.</param>
    /// <returns>A task representing the complete action.</returns>
    Task ExecuteAlternateAsync(
        SoftKeyboardActionContext context,
        string alternative,
        CancellationToken cancellationToken);
}