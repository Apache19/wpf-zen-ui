// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Provides routed commands for candidate and input-method surfaces hosted by a <see cref="SoftKeyboard"/>.
/// </summary>
public static class SoftKeyboardCommands
{
    /// <summary>
    /// Gets the command that commits the <see cref="SoftKeyboardCandidate"/> supplied as its parameter.
    /// </summary>
    public static RoutedUICommand CommitCandidate { get; }
        = new RoutedUICommand(
            "Commit candidate",
            nameof(CommitCandidate),
            typeof(SoftKeyboardCommands)
        );

    /// <summary>
    /// Gets the command that moves to the previous candidate page.
    /// </summary>
    public static RoutedUICommand MoveToPreviousCandidatePage { get; }
        = new RoutedUICommand(
            "Previous candidate page",
            nameof(MoveToPreviousCandidatePage),
            typeof(SoftKeyboardCommands)
        );

    /// <summary>
    /// Gets the command that moves to the next candidate page.
    /// </summary>
    public static RoutedUICommand MoveToNextCandidatePage { get; }
        = new RoutedUICommand(
            "Next candidate page",
            nameof(MoveToNextCandidatePage),
            typeof(SoftKeyboardCommands)
        );

    /// <summary>
    /// Gets the command that activates the <see cref="SoftKeyboardInputMethod"/> supplied as its parameter.
    /// </summary>
    public static RoutedUICommand ActivateInputMethod { get; }
        = new RoutedUICommand(
            "Activate input method",
            nameof(ActivateInputMethod),
            typeof(SoftKeyboardCommands)
        );
}
