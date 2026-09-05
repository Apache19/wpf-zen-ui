// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Receives presentation lifecycle notifications in a dialog view or view model without
/// subscribing to control events. Use the opening callback for cancellable initialization.
/// </summary>
/// <remarks>
/// The host considers the dialog, its DataContext, its direct Content and that content's
/// FrameworkElement.DataContext, in that order, and notifies each distinct instance once.
/// Participants are captured after loading and entrance; template descendants are not searched.
/// Changing content or DataContext during presentation does not change these participants.
/// Callbacks start on the host dispatcher and describe a presentation, independently of Loaded
/// and Unloaded. A presentation interrupted before opening receives neither callback.
/// </remarks>
public interface IContentDialogAware
{
    /// <summary>
    /// Starts work after the dialog has loaded and finished its entrance, before its Opened event.
    /// </summary>
    /// <param name="cancellationToken">
    /// A fresh token for this presentation. An accepted close request, external cancellation or
    /// forced host cleanup cancels it before the closing notification. A vetoed close does not.
    /// Pass it to asynchronous operations and check it before publishing results after an await.
    /// </param>
    /// <returns>A non-null task representing initialization or work for this presentation.</returns>
    /// <remarks>
    /// The host observes the task without waiting for it to finish before accepting interaction
    /// or closing. Cooperative cancellation is ignored. Other failures end an active presentation
    /// and fault ShowAsync; failures arriving after its outcome is settled are traced only and
    /// cannot affect a subsequent presentation. Handle recoverable business errors locally.
    /// </remarks>
    Task OnDialogOpenedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Releases presentation-specific state after logical detachment and input release, before
    /// the Closed event and completion of ShowAsync. This notification cannot veto closure.
    /// </summary>
    /// <param name="result">
    /// The accepted business result, or null for dismissal, cancellation or removal without a result.
    /// </param>
    /// <remarks>
    /// Only participants whose opening callback was invoked receive this notification, including
    /// callbacks that failed or are still completing. The opening token is already cancelled;
    /// closure does not wait for work that ignores cancellation. The dialog remains reserved until
    /// all closing notifications finish. Exceptions fault ShowAsync without preventing cleanup
    /// or notification of the other participants.
    /// </remarks>
    void OnDialogClosed(object? result);
}
