// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes the visual variant requested by the control presenting an icon.
/// </summary>
/// <remarks>
/// A variant is presentation context rather than an icon identity. Icon families may interpret a
/// variant differently or ignore variants they do not support. For example, Fluent System Icons
/// maps <see cref="Filled"/> to its filled font, while a bitmap-only icon source can continue to
/// display the same image.
/// </remarks>
public enum IconVariant
{
    /// <summary>
    /// Uses the variant selected by the icon source.
    /// </summary>
    Default,

    /// <summary>
    /// Requests the regular or outline representation when the icon family provides one.
    /// </summary>
    Regular,

    /// <summary>
    /// Requests the filled representation when the icon family provides one.
    /// </summary>
    Filled,
}
