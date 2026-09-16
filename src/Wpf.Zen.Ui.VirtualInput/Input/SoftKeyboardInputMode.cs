// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes semantic input-mode state interpreted by an application or extension.
/// </summary>
/// <remarks>
/// A mode is separate from the active input-method profile and from the native language-bar
/// command. Presentation and transition capabilities are resolved independently so this type
/// cannot accidentally select a command route or override a mode-key label.
/// </remarks>
public sealed class SoftKeyboardInputMode : IEquatable<SoftKeyboardInputMode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputMode"/> class.
    /// </summary>
    /// <param name="id">
    /// A stable, adapter-defined identifier for the mode, such as <c>direct</c> or <c>hiragana</c>.
    /// </param>
    /// <param name="isNativeInput">
    /// Whether character keys currently pass through a native-language conversion pipeline.
    /// </param>
    public SoftKeyboardInputMode(
        string id,
        bool isNativeInput)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("An input-mode identifier is required.", nameof(id));

        IsNativeInput = isNativeInput;
    }

    /// <summary>
    /// Gets the stable identifier assigned by the adapter that interpreted the native state.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets whether character input currently uses native-language conversion.
    /// </summary>
    /// <remarks>
    /// This value controls native layouts and candidate ownership. It does not mean that the input
    /// method profile is active; profile selection is represented by
    /// <see cref="SoftKeyboardInputMethod.IsActive"/>.
    /// </remarks>
    public bool IsNativeInput { get; }

    /// <inheritdoc />
    public bool Equals(SoftKeyboardInputMode? other)
    {
        return other is not null
            && Id == other.Id
            && IsNativeInput == other.IsNativeInput;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as SoftKeyboardInputMode);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(Id);
            return (hash * 397) ^ IsNativeInput.GetHashCode();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Id;
    }
}
