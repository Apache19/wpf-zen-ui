// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Describes whether a candidate operation was handled and whether it succeeded.
/// </summary>
public sealed class SoftKeyboardCandidateOperationResult
{
    private SoftKeyboardCandidateOperationResult(bool handled, bool succeeded, Exception? error)
    {
        Handled   = handled;
        Succeeded = succeeded;
        Error     = error;
    }

    /// <summary>
    /// Gets the shared result that delegates an operation to the default candidate implementation.
    /// </summary>
    public static SoftKeyboardCandidateOperationResult NotHandled { get; } = new(false, false, null);

    /// <summary>
    /// Gets the shared successful result.
    /// </summary>
    public static SoftKeyboardCandidateOperationResult Success { get; } = new(true, true, null);

    /// <summary>
    /// Gets a value indicating whether the provider made the operation decision.
    /// </summary>
    public bool Handled { get; }

    /// <summary>
    /// Gets a value indicating whether a handled operation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the failure diagnostic, or null for a successful or unhandled operation.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Creates a handled failure result.
    /// </summary>
    /// <param name="error">The diagnostic retained by the keyboard.</param>
    /// <returns>A failed operation result.</returns>
    public static SoftKeyboardCandidateOperationResult Failure(Exception error)
    {
        return new SoftKeyboardCandidateOperationResult(
            true,
            false,
            error ?? throw new ArgumentNullException(nameof(error))
        );
    }
}