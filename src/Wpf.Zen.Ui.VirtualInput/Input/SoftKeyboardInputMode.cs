// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes service-specific input mode state interpreted by an application or extension.
/// </summary>
/// <remarks>
/// A mode is separate from the active input-method profile and from the native language-bar
/// command. The Windows source does not create this model from generic TSF compartment values.
/// An input-mode adapter may publish it only when it understands the active service's contract.
/// Button presentation remains independent unless the adapter explicitly supplies a replacement.
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
    /// <param name="canSwitch">
    /// Whether the current adapter or source can request a transition away from this mode.
    /// </param>
    /// <param name="button">
    /// An optional replacement for the platform input-mode button presentation.
    /// </param>
    public SoftKeyboardInputMode(
        string id,
        bool isNativeInput,
        bool canSwitch,
        SoftKeyboardInputModeButton? button = null)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("An input-mode identifier is required.", nameof(id));

        IsNativeInput = isNativeInput;
        CanSwitch     = canSwitch;
        Button        = button;
    }

    /// <summary>
    /// Gets the stable identifier assigned by the adapter that interpreted the native state.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the optional adapter-defined replacement for the platform button presentation.
    /// </summary>
    /// <remarks>
    /// Null keeps the button supplied by the active input-method session. An adapter should set
    /// this only when it understands the service-specific presentation contract.
    /// </remarks>
    public SoftKeyboardInputModeButton? Button { get; }

    /// <summary>
    /// Gets whether character input currently uses native-language conversion.
    /// </summary>
    /// <remarks>
    /// This value controls native layouts and candidate ownership. It does not mean that the input
    /// method profile is active; profile selection is represented by
    /// <see cref="SoftKeyboardInputMethod.IsActive"/>.
    /// </remarks>
    public bool IsNativeInput { get; }

    /// <summary>
    /// Gets whether the current adapter or source can request a transition from this mode.
    /// </summary>
    public bool CanSwitch { get; }

    /// <inheritdoc />
    public bool Equals(SoftKeyboardInputMode? other)
    {
        return other is not null
            && Id == other.Id
            && IsNativeInput == other.IsNativeInput
            && CanSwitch == other.CanSwitch
            && Equals(Button, other.Button);
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
            hash = (hash * 397) ^ IsNativeInput.GetHashCode();
            hash = (hash * 397) ^ CanSwitch.GetHashCode();
            return (hash * 397) ^ (Button?.GetHashCode() ?? 0);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Id;
    }
}
