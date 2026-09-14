// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Connects public routed commands to the keyboard's validated asynchronous operations.
/// </summary>
public partial class SoftKeyboard
{
    private static void RegisterCommands()
    {
        CommandManager.RegisterClassCommandBinding(
            typeof(SoftKeyboard),
            new CommandBinding(
                SoftKeyboardCommands.CommitCandidate,
                OnCommitCandidateCommandExecuted,
                OnCommitCandidateCommandCanExecute
            )
        );

        CommandManager.RegisterClassCommandBinding(
            typeof(SoftKeyboard),
            new CommandBinding(
                SoftKeyboardCommands.MoveToPreviousCandidatePage,
                OnPreviousCandidatePageCommandExecuted,
                OnPreviousCandidatePageCommandCanExecute
            )
        );

        CommandManager.RegisterClassCommandBinding(
            typeof(SoftKeyboard),
            new CommandBinding(
                SoftKeyboardCommands.MoveToNextCandidatePage,
                OnNextCandidatePageCommandExecuted,
                OnNextCandidatePageCommandCanExecute
            )
        );

        CommandManager.RegisterClassCommandBinding(
            typeof(SoftKeyboard),
            new CommandBinding(
                SoftKeyboardCommands.ActivateInputMethod,
                OnActivateInputMethodCommandExecuted,
                OnActivateInputMethodCommandCanExecute
            )
        );
    }

    private static void OnCommitCandidateCommandCanExecute(object sender, CanExecuteRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        args.CanExecute = args.Parameter is SoftKeyboardCandidate candidate
            && keyboard.IsInputSurfaceAvailable
            && keyboard.CanPresentCandidates
            && keyboard._candidates.Contains(candidate);

        args.Handled = true;
    }

    private static void OnCommitCandidateCommandExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        if (args.Parameter is SoftKeyboardCandidate candidate)
        {
            keyboard.CandidateInteractionVersion++;
            _ = keyboard.CommitCandidateAsync(candidate);
            keyboard.RequestSystemKeyboardHide();
        }

        args.Handled = true;
    }

    private static void OnPreviousCandidatePageCommandCanExecute(object sender, CanExecuteRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        args.CanExecute = keyboard.IsInputSurfaceAvailable
            && keyboard.CanPresentCandidates
            && keyboard.CanGoToPreviousCandidatePage;

        args.Handled = true;
    }

    private static void OnPreviousCandidatePageCommandExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        ((SoftKeyboard)sender).MoveCandidatePage(-1);
        args.Handled = true;
    }

    private static void OnNextCandidatePageCommandCanExecute(object sender, CanExecuteRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        args.CanExecute = keyboard.IsInputSurfaceAvailable
            && keyboard.CanPresentCandidates
            && keyboard.CanGoToNextCandidatePage;

        args.Handled = true;
    }

    private static void OnNextCandidatePageCommandExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        ((SoftKeyboard)sender).MoveCandidatePage(1);
        args.Handled = true;
    }

    private static void OnActivateInputMethodCommandCanExecute(object sender, CanExecuteRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        args.CanExecute = args.Parameter is SoftKeyboardInputMethod inputMethod
            && keyboard.IsInputMethodMenuAvailable
            && keyboard.InputMethods.Any(item => HaveSameInputMethod(item, inputMethod));

        args.Handled = true;
    }

    private static void OnActivateInputMethodCommandExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        var keyboard = (SoftKeyboard)sender;

        if (args.Parameter is SoftKeyboardInputMethod inputMethod)
        {
            keyboard.CandidateInteractionVersion++;
            keyboard.ActivateInputMethod(inputMethod);
        }

        args.Handled = true;
    }

    private bool IsInputSurfaceAvailable => IsOpen && IsEnabled && IsVisible && ActiveTarget is not null;
}
