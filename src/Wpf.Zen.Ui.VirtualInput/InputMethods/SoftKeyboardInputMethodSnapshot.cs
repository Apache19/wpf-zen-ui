// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Carries one immutable input-method catalog and its active profile.
/// </summary>
public sealed class SoftKeyboardInputMethodSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputMethodSnapshot"/> class.
    /// </summary>
    /// <param name="inputMethods">The enabled profiles in presentation order.</param>
    /// <param name="activeInputMethod">The active profile, or null.</param>
    public SoftKeyboardInputMethodSnapshot(
        IEnumerable<SoftKeyboardInputMethod> inputMethods,
        SoftKeyboardInputMethod? activeInputMethod)
    {
        var items = inputMethods?.ToArray()
            ?? throw new ArgumentNullException(nameof(inputMethods));

        if (items.Any(static item => item is null))
        {
            throw new ArgumentException(
                "An input method cannot be null.", nameof(inputMethods)
            );
        }

        InputMethods = new ReadOnlyCollection<SoftKeyboardInputMethod>(items);

        ActiveInputMethod = activeInputMethod;
    }

    /// <summary>
    /// Gets the enabled profiles in presentation order.
    /// </summary>
    public IReadOnlyList<SoftKeyboardInputMethod> InputMethods { get; }

    /// <summary>
    /// Gets the active profile, or null when none was identified.
    /// </summary>
    public SoftKeyboardInputMethod? ActiveInputMethod { get; }
}