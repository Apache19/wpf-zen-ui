// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Separates command discovery from execution so a failed invocation is never retried by an
/// adapter.
/// </summary>
public enum SoftKeyboardInputModeCommandResult
{
    /// <summary>
    /// No executable command was found; an extension may provide a fallback.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The command was invoked and accepted the request, without confirming a state change.
    /// </summary>
    Accepted,

    /// <summary>
    /// The command was invoked but failed. No second command should be attempted.
    /// </summary>
    Failed,
}
