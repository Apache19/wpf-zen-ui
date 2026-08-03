// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines the visual appearance of a control.
///
/// <para>
/// This enumeration only controls the <b>visual style</b> of a control.
/// It does not represent action importance or interaction semantics.
/// </para>
///
/// <para>
/// The values are divided into two categories:
/// </para>
///
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Weak semantic appearances</b> (Normal, Transparent, Info, Dark, Light)
/// <br/>
/// These values represent neutral or stylistic variations.
/// They can be overridden by control state (for example, a default button).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Strong semantic appearances</b> (Danger, Success, Caution)
/// <br/>
/// These values represent explicit business meaning and always take precedence
/// over control state. They should not be overridden by implicit styling logic.
/// </description>
/// </item>
/// </list>
///
/// <para>
/// For example:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// A button with <see cref="System.Windows.Controls.Button.IsDefault"/> may be
/// visually emphasized when using weak semantic appearances.
/// </description>
/// </item>
/// <item>
/// <description>
/// A button with <see cref="Danger"/> will always use the danger style,
/// even if it is the default button.
/// </description>
/// </item>
/// </list>
/// </summary>
public enum ControlAppearance
{
    /// <summary>
    /// Default neutral appearance.
    /// This is the base style used when no specific visual emphasis is required.
    /// </summary>
    Normal,

    /// <summary>
    /// An accent-colored appearance using the application's theme accent color.
    /// Provides visual emphasis without implying semantic meaning.
    /// </summary>
    Accent,

    /// <summary>
    /// Transparent appearance with minimal visual background.
    /// Useful for lightweight or inline actions.
    /// </summary>
    Transparent,

    /// <summary>
    /// Informational appearance using a blue color scheme.
    /// Typically used for neutral informational actions.
    /// </summary>
    Info,

    /// <summary>
    /// Dark appearance.
    /// Suitable for contrast on light backgrounds or stylistic emphasis.
    /// </summary>
    Dark,

    /// <summary>
    /// Light appearance.
    /// Suitable for contrast on dark backgrounds.
    /// </summary>
    Light,

    /// <summary>
    /// Indicates a destructive or dangerous action.
    /// This is a strong semantic appearance and always takes visual priority.
    /// </summary>
    Danger,

    /// <summary>
    /// Indicates a successful or positive action.
    /// This is a strong semantic appearance and always takes visual priority.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates a warning or cautionary action.
    /// This is a strong semantic appearance and always takes visual priority.
    /// </summary>
    Caution,
}