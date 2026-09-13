// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Implements a cancellable custom key action whose guarded context remains valid until completion.
/// </summary>
public interface IAsyncSoftKeyboardKeyAction : ISoftKeyboardAction
{
    /// <summary>
    /// Executes the function using the current keyboard's guarded services.
    /// </summary>
    /// <param name="context">Services that expire when the task completes or loses ownership.</param>
    /// <param name="cancellationToken">Cancels work after a newer input operation takes ownership.</param>
    /// <returns>A task representing the complete action.</returns>
    Task ExecuteAsync(SoftKeyboardActionContext context, CancellationToken cancellationToken);
}