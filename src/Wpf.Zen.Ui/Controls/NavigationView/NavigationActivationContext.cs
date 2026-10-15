// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents one page activation, including subsequent commands and background work in scope stop
/// completion.
/// </summary>
/// <remarks>
/// Cache reentry or visual reattachment creates a new context. Call RunAsync on the UI thread,
/// and include all asynchronous work using page dependencies in its delegate. Unregistered
/// background tasks are outside the completion guarantee.
/// </remarks>
public sealed class NavigationActivationContext
{
    private readonly NavigationActivation _activation;
    private readonly object               _participant;

    internal NavigationActivationContext(NavigationActivation activation, object participant)
    {
        _activation  = activation;
        _participant = participant;
    }

    /// <summary>
    /// Gets the activation token, cancelled on page departure, unload, or scope stop.
    /// </summary>
    public CancellationToken CancellationToken => _activation.Token;

    /// <summary>
    /// Gets whether new operations may start; remains false permanently once this activation stops.
    /// </summary>
    public bool IsActive => !_activation.IsStopping;

    /// <summary>
    /// Gets this activation's semantic target, or null for content replacement without a semantic
    /// target.
    /// </summary>
    public NavigationTarget? Target => _activation.Target;

    /// <summary>
    /// Registers completion responsibility before execution; stopping waits for the operation and
    /// its error delivery.
    /// </summary>
    /// <param name="operation">
    /// The complete operation using this activation's cancellation token.
    /// </param>
    /// <returns>
    /// The operation task; exceptions are also reported through the navigation error event.
    /// </returns>
    /// <exception cref="ObjectDisposedException">This activation has stopped.</exception>
    public Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        return _activation.RunOperationAsync(_participant, operation);
    }
}

/// <summary>
/// Receives a per-activation context for page models with asynchronous commands after
/// initialization.
/// </summary>
/// <remarks>
/// Takes precedence over the older asynchronous interface when both are implemented. Views enter
/// before models; models exit before views.
/// Callbacks may request scope stop but must not await stop completion that includes themselves.
/// </remarks>
public interface INavigationActivationAware : INavigationExited
{
    /// <summary>
    /// Initializes this activation and retains its context for registering subsequent operations.
    /// </summary>
    /// <param name="context">
    /// The context exclusive to this activation; never reuse across activations.
    /// </param>
    /// <returns>
    /// The initialization task; completion does not end the page instance's lifetime.
    /// </returns>
    Task OnNavigationEnteredAsync(NavigationActivationContext context);
}