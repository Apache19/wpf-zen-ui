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
    /// <param name="classId">
    /// The TSF service identifier, or empty for a keyboard layout or language-only entry.
    /// </param>
    /// <param name="profileId">
    /// The TSF profile identifier, or empty for a keyboard layout or language-only entry.
    /// </param>
    /// <param name="languageId">The nonzero Windows language identifier.</param>
    /// <param name="displayName">The nonempty localized presentation name.</param>
    /// <param name="isActive">
    /// Whether this profile was active when its source was refreshed.
    /// </param>
    /// <param name="touchLayoutPreference">
    /// The optional touch-layout capability reported by the profile.
    /// </param>
    /// <param name="supportsImeToggle">
    /// Whether the source supports native/direct mode changes for this entry. Defaults to true for
    /// TSF profiles and false for language-only entries; sources can override either default.
    /// </param>
    /// <param name="keyboardLayout">
    /// The exact Windows keyboard-layout handle, or zero for a TIP or language-only fallback.
    /// Layout handles distinguish multiple keyboard layouts within the same language.
    /// </param>
    public SoftKeyboardInputMethod(
        Guid classId,
        Guid profileId,
        ushort languageId,
        string displayName,
        bool isActive,
        SoftKeyboardTouchLayoutPreference? touchLayoutPreference = null,
        bool? supportsImeToggle = null,
        IntPtr keyboardLayout = default)
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

        if (classId != Guid.Empty && keyboardLayout != IntPtr.Zero)
        {
            throw new ArgumentException("A TIP profile cannot also identify a keyboard layout.");
        }

        ClassId        = classId;
        ProfileId      = profileId;
        LanguageId     = languageId;
        DisplayName    = displayName;
        IsActive       = isActive;
        KeyboardLayout = keyboardLayout;

        TouchLayoutPreference = touchLayoutPreference;
        SupportsImeToggle     = supportsImeToggle ?? classId != Guid.Empty;
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
    /// Gets the exact Windows keyboard-layout handle, or zero for a TIP or language-only entry.
    /// The handle is valid in the current Windows session and must not be persisted as a
    /// preference.
    /// </summary>
    public IntPtr KeyboardLayout { get; }

    /// <summary>
    /// Gets the localized profile name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the language and region name localized for the current UI culture. Each profile keeps
    /// its own identity even when several profiles share this presentation label.
    /// </summary>
    public string LanguageDisplayName
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(LanguageId).DisplayName;
            }
            catch (CultureNotFoundException)
            {
                return $"0x{LanguageId:X4}";
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this profile was active when the list was refreshed.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets whether this entry supports switching between native and direct input.
    /// Sources must set this to false for text services without an open/close mode contract.
    /// This capability is independent of the current mode and input language.
    /// </summary>
    public bool SupportsImeToggle { get; }

    /// <summary>
    /// Gets the active TIP's preferred Windows touch layout, or null when querying is unavailable.
    /// </summary>
    /// <remarks>
    /// Only active profiles are queried. Unknown identifiers remain available to application
    /// layout policies.
    /// </remarks>
    public SoftKeyboardTouchLayoutPreference? TouchLayoutPreference { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}