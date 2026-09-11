// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Represents one candidate supplied by the active Windows input method.
/// </summary>
public sealed class SoftKeyboardCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardCandidate"/> class.
    /// </summary>
    /// <param name="index">
    /// The input method index, not the page-relative position.
    /// </param>
    /// <param name="text">
    /// The candidate text.
    /// </param>
    public SoftKeyboardCandidate(uint index, string text)
    {
        Index = index;
        Text  = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Gets the zero-based candidate index understood by the input method.
    /// </summary>
    public uint Index { get; }

    /// <summary>
    /// Gets the candidate text.
    /// </summary>
    public string Text { get; }
}

