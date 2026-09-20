// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes one enabled Windows input method or keyboard layout.
/// </summary>
/// <remarks>
/// To activate an enumerated input method, pass this instance to
/// <see cref="Controls.SoftKeyboard.TryActivateInputMethod"/>. Windows input-method sources retain
/// the native identity needed to select an exact keyboard layout within the same language.
/// </remarks>
public sealed class SoftKeyboardInputMethod
{
    /// <summary>
    /// Creates a TSF profile or language-only input-method description for a custom source.
    /// </summary>
    /// <param name="classId">
    /// The TSF service identifier, or empty for a language-only entry.
    /// </param>
    /// <param name="profileId">
    /// The TSF profile identifier, or empty for a language-only entry.
    /// </param>
    /// <param name="languageId">The nonzero Windows language identifier.</param>
    /// <param name="displayName">The nonempty localized presentation name.</param>
    /// <param name="isActive">
    /// Whether this profile was active when its source was refreshed.
    /// </param>
    /// <param name="touchLayoutPreference">
    /// The optional touch-layout capability reported by the profile.
    /// </param>
    public SoftKeyboardInputMethod(
        Guid classId,
        Guid profileId,
        ushort languageId,
        string displayName,
        bool isActive,
        SoftKeyboardTouchLayoutPreference? touchLayoutPreference = null)
        : this(classId, profileId, languageId, displayName, isActive, IntPtr.Zero, touchLayoutPreference)
    {
    }

    /// <summary>
    /// Creates a description retaining the native identity supplied by Windows profile discovery.
    /// </summary>
    /// <param name="classId">The TSF service identifier, or empty for a non-TIP entry.</param>
    /// <param name="profileId">The TSF profile identifier, or empty for a non-TIP entry.</param>
    /// <param name="languageId">The nonzero Windows language identifier.</param>
    /// <param name="displayName">The nonempty localized presentation name.</param>
    /// <param name="isActive">Whether the profile was active when its source was refreshed.</param>
    /// <param name="keyboardLayout">
    /// The exact Windows keyboard-layout handle, or zero for a TIP or language-only entry.
    /// Layout handles distinguish multiple keyboard layouts within the same language.
    /// </param>
    /// <param name="touchLayoutPreference">
    /// The optional touch-layout capability reported by the profile.
    /// </param>
    internal SoftKeyboardInputMethod(
        Guid classId,
        Guid profileId,
        ushort languageId,
        string displayName,
        bool isActive,
        IntPtr keyboardLayout,
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
    /// </summary>
    /// <remarks>
    /// Used by profile activation and identity comparison to distinguish layouts in one language.
    /// This borrowed handle is valid in the current Windows session; it must not be released or
    /// persisted as a preference.
    /// </remarks>
    internal IntPtr KeyboardLayout { get; }

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
