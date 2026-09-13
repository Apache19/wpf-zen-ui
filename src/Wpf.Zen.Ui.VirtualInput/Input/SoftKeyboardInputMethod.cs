// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes one enabled Windows input method or keyboard layout.
/// </summary>
public sealed class SoftKeyboardInputMethod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputMethod"/> class.
    /// </summary>
    /// <param name="classId">The TSF text-service class identifier, or empty for a language-only entry.</param>
    /// <param name="profileId">The TSF language-profile identifier, or empty for a language-only entry.</param>
    /// <param name="languageId">The nonzero Windows language identifier.</param>
    /// <param name="displayName">The nonempty localized presentation name.</param>
    /// <param name="isActive">Whether this profile was active when its source was refreshed.</param>
    /// <param name="touchLayoutPreference">The optional touch-layout capability reported by the profile.</param>
    public SoftKeyboardInputMethod(
        Guid   classId,
        Guid   profileId,
        ushort languageId,
        string displayName,
        bool   isActive,
        SoftKeyboardTouchLayoutPreference? touchLayoutPreference = null)
    {
        if (languageId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(languageId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("An input-method display name is required.", nameof(displayName));
        }

        if ((classId == Guid.Empty) != (profileId == Guid.Empty))
        {
            throw new ArgumentException(
                "A TSF class identifier and profile identifier must either both be empty or both be present."
            );
        }

        ClassId     = classId;
        ProfileId   = profileId;
        LanguageId  = languageId;
        DisplayName = displayName;
        IsActive    = isActive;

        TouchLayoutPreference = touchLayoutPreference;
    }

    /// <summary>
    /// Gets the TSF text-service class identifier.
    /// </summary>
    public Guid ClassId { get; }

    /// <summary>
    /// Gets the TSF language-profile identifier.
    /// </summary>
    public Guid ProfileId { get; }

    /// <summary>
    /// Gets the Windows language identifier.
    /// </summary>
    public ushort LanguageId { get; }

    /// <summary>
    /// Gets the localized profile name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether this profile was active when the list was refreshed.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the active TIP's preferred Windows touch layout, or null when querying is unavailable.
    /// </summary>
    /// <remarks>
    /// Only active profiles are queried. Unknown identifiers remain available to application layout policies.
    /// </remarks>
    public SoftKeyboardTouchLayoutPreference? TouchLayoutPreference { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}
