// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Describes the outcome of a navigation request.
/// </summary>
public enum NavigationResultStatus
{
    /// <summary>
    /// The request was accepted; await Completion to determine the Frame commit outcome.
    /// </summary>
    Accepted = 4,

    /// <summary>
    /// The caller, host, or Frame cancelled the request before commit.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The host superseded the uncommitted request; NavigationView serializes all accepted requests
    /// by default.
    /// </summary>
    Superseded,

    /// <summary>
    /// Preparation or commit projection failed; Failure stores the exception and WasCommitted
    /// records Frame commit.
    /// </summary>
    Failed,

    /// <summary>
    /// The host rejected or had no state change for the request.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// The target committed successfully; asynchronous page initialization may still be running.
    /// </summary>
    Succeeded,

    /// <summary>
    /// No navigation item matched a requested id or tag.
    /// </summary>
    TargetNotFound,

    /// <summary>
    /// No live named navigation scope matched an explicit routing request.
    /// </summary>
    HostNotFound,
}

/// <summary>
/// Contains the status returned by a navigation operation.
/// </summary>
public readonly struct NavigationResult
{
    private readonly Task<NavigationResult>? _completion;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationResult"/> struct.
    /// </summary>
    /// <param name="status">The completed operation status.</param>
    public NavigationResult(NavigationResultStatus status)
    {
        if (!Enum.IsDefined(typeof(NavigationResultStatus), status)
            || status is NavigationResultStatus.Accepted or NavigationResultStatus.Failed)
        {
            throw new ArgumentException(
                "Pending and failed results require their completion or failure data.",
                nameof(status)
            );
        }

        Status       = status;
        WasCommitted = status == NavigationResultStatus.Succeeded;
    }

    private NavigationResult(Task<NavigationResult> completion)
    {
        Status      = NavigationResultStatus.Accepted;
        _completion = completion;
    }

    private NavigationResult(Exception failure, bool wasCommitted)
    {
        Status       = NavigationResultStatus.Failed;
        Failure      = failure;
        WasCommitted = wasCommitted;
    }

    /// <summary>
    /// Gets whether the request was accepted or committed successfully; acceptance alone does not
    /// establish page display.
    /// </summary>
    public bool WasAccepted
    {
        get => Status is NavigationResultStatus.Accepted or NavigationResultStatus.Succeeded;
    }

    /// <summary>
    /// Gets whether the final result involved actual Frame commit; this may be true even if
    /// projection notification failed.
    /// </summary>
    public bool WasCommitted { get; }

    /// <summary>
    /// Gets the request failure; asynchronous page initialization errors use lifecycle error events
    /// and scope completion.
    /// </summary>
    public Exception? Failure { get; }

    /// <summary>
    /// Gets final Frame commit completion, including cancellation, supersession, and failure,
    /// without waiting for page initialization.
    /// </summary>
    public Task<NavigationResult> Completion
    {
        get => _completion ?? Task.FromResult(this);
    }

    /// <summary>
    /// Creates a pending result so host implementations can publish stable commit completion.
    /// </summary>
    /// <param name="completion">
    /// The final task, reflecting actual commit and never returning Accepted again.
    /// </param>
    /// <returns>An accepted result with awaitable completion.</returns>
    public static NavigationResult Pending(Task<NavigationResult> completion)
    {
        if (completion is null)
        {
            throw new ArgumentNullException(nameof(completion));
        }

        return new NavigationResult(completion);
    }

    /// <summary>
    /// Creates a failure result while preserving whether Frame committed.
    /// </summary>
    /// <param name="failure">The original failure or an aggregate of independent failures.</param>
    /// <param name="wasCommitted">
    /// Whether Frame had committed the target when failure occurred.
    /// </param>
    /// <returns>The final result with failure details.</returns>
    public static NavigationResult FromFailure(Exception failure, bool wasCommitted)
    {
        if (failure is null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        return new NavigationResult(failure, wasCommitted);
    }

    /// <summary>
    /// Gets the completed operation status.
    /// </summary>
    public NavigationResultStatus Status { get; }

    /// <summary>
    /// Gets a value indicating whether navigation was performed successfully.
    /// </summary>
    public bool Succeeded
    {
        get => Status == NavigationResultStatus.Succeeded;
    }

    /// <summary>
    /// Gets a successful navigation result.
    /// </summary>
    public static NavigationResult Success
    {
        get => new(NavigationResultStatus.Succeeded);
    }

    /// <summary>
    /// Gets a result indicating that the host did not navigate.
    /// </summary>
    public static NavigationResult Rejected
    {
        get => new(NavigationResultStatus.Rejected);
    }

    /// <summary>
    /// Gets a result indicating that a target id or tag was not registered.
    /// </summary>
    public static NavigationResult TargetNotFound
    {
        get => new(NavigationResultStatus.TargetNotFound);
    }

    /// <summary>
    /// Gets a result indicating that an explicitly named host is not available.
    /// </summary>
    public static NavigationResult HostNotFound
    {
        get => new(NavigationResultStatus.HostNotFound);
    }
}