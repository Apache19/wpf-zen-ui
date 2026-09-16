// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Identifies an adapter-defined transition that can be requested from the current input mode.
/// </summary>
/// <remarks>
/// The descriptor contains no delegate and does not retain UI state. The keyboard stores the
/// adapter that supplied it and invokes that same adapter when the user presses the mode key.
/// </remarks>
public sealed class SoftKeyboardInputModeTransition : IEquatable<SoftKeyboardInputModeTransition>
{
    /// <summary>
    /// Initializes a new transition descriptor.
    /// </summary>
    /// <param name="id">A stable adapter-defined identifier for the transition.</param>
    public SoftKeyboardInputModeTransition(string id)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("A transition identifier is required.", nameof(id));
    }

    /// <summary>
    /// Gets the stable adapter-defined transition identifier.
    /// </summary>
    public string Id { get; }

    /// <inheritdoc />
    public bool Equals(SoftKeyboardInputModeTransition? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as SoftKeyboardInputModeTransition);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Id);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Id;
    }
}
