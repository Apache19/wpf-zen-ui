// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Collects replaceable platform boundaries used by one soft-keyboard instance.
/// </summary>
/// <remarks>
/// The aggregate is internal because backend replacement is a test seam, not an application extension
/// point. Keeping the four contracts separate prevents native input, input-pane, profile, and candidate
/// responsibilities from becoming coupled merely because the control consumes all of them.
/// </remarks>
internal sealed class SoftKeyboardPlatformServices
{
    internal static SoftKeyboardPlatformServices Default { get; } = new(
        new WindowsInputBackend(),
        new WindowsInputPaneBackend(),
        new TsfInputMethodService(),
        new TsfCandidateBackend()
    );

    internal SoftKeyboardPlatformServices(
        ISoftKeyboardInputBackend input,
        ISoftKeyboardInputPaneBackend inputPane,
        ISoftKeyboardInputMethodBackend inputMethods,
        ISoftKeyboardCandidateBackend candidates)
    {
        Input        = input ?? throw new ArgumentNullException(nameof(input));
        InputPane    = inputPane ?? throw new ArgumentNullException(nameof(inputPane));
        InputMethods = inputMethods ?? throw new ArgumentNullException(nameof(inputMethods));
        Candidates   = candidates ?? throw new ArgumentNullException(nameof(candidates));
    }

    internal ISoftKeyboardInputBackend Input { get; }

    internal ISoftKeyboardInputPaneBackend InputPane { get; }

    internal ISoftKeyboardInputMethodBackend InputMethods { get; }

    internal ISoftKeyboardCandidateBackend Candidates { get; }
}
