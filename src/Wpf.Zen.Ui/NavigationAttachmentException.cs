// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Indicates attachment failure requiring asynchronous rollback; await Completion to observe all
/// cleanup failures.
/// </summary>
public sealed class NavigationAttachmentException : InvalidOperationException
{
    internal NavigationAttachmentException(Exception failure, Task completion)
        : base("Navigation attachment failed; asynchronous rollback is still in progress.", failure)
    {
        Completion = completion;
    }

    /// <summary>
    /// Gets rollback completion, including the original attachment failure and independent cleanup
    /// failures.
    /// </summary>
    public Task Completion { get; }
}
