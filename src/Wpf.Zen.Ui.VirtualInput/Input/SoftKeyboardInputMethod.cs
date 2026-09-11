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
    internal SoftKeyboardInputMethod(
        Guid classId,
        Guid profileId,
        ushort languageId,
        string displayName,
        bool isActive)
    {
        ClassId     = classId;
        ProfileId   = profileId;
        LanguageId  = languageId;
        DisplayName = displayName;
        IsActive    = isActive;
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

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}
