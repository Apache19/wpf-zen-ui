// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes the active input method's interpreted mode for presentation and keyboard routing.
/// </summary>
/// <remarks>
/// A mode is separate from the active input-method profile. Text services can expose several modes
/// through TSF compartments, and the meaning of a native value belongs to that text service. An
/// input-mode adapter translates those values into this service-independent description.
/// </remarks>
public sealed class SoftKeyboardInputMode : IEquatable<SoftKeyboardInputMode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputMode"/> class.
    /// </summary>
    /// <param name="id">
    /// A stable, adapter-defined identifier for the mode, such as <c>direct</c> or <c>hiragana</c>.
    /// </param>
    /// <param name="keyLabel">The short text displayed on the input-mode key.</param>
    /// <param name="displayName">The accessible, user-facing name of the current mode.</param>
    /// <param name="isNativeInput">
    /// Whether character keys currently pass through a native-language conversion pipeline.
    /// </param>
    /// <param name="canSwitch">
    /// Whether the current adapter or source can request a transition away from this mode.
    /// </param>
    public SoftKeyboardInputMode(
        string id,
        string keyLabel,
        string displayName,
        bool isNativeInput,
        bool canSwitch)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("An input-mode identifier is required.", nameof(id));

        KeyLabel = !string.IsNullOrWhiteSpace(keyLabel)
            ? keyLabel
            : throw new ArgumentException("An input-mode key label is required.", nameof(keyLabel));

        DisplayName = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : throw new ArgumentException("An input-mode display name is required.", nameof(displayName));

        IsNativeInput = isNativeInput;
        CanSwitch     = canSwitch;
    }

    /// <summary>
    /// Gets the stable identifier assigned by the adapter that interpreted the native state.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the compact label displayed on the input-mode key.
    /// </summary>
    public string KeyLabel { get; }

    /// <summary>
    /// Gets the user-facing mode name used by automation and other descriptive UI.
    /// </summary>
    public string DisplayName { get; }

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
            && KeyLabel == other.KeyLabel
            && DisplayName == other.DisplayName
            && IsNativeInput == other.IsNativeInput
            && CanSwitch == other.CanSwitch;
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
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(KeyLabel);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName);
            hash = (hash * 397) ^ IsNativeInput.GetHashCode();
            return (hash * 397) ^ CanSwitch.GetHashCode();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}
