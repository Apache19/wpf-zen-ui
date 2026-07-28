// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Identifies reusable control and data templates exposed by the WPF UI theme dictionaries.
/// </summary>
/// <remarks>
/// Templates are kept separate from <see cref="StyleToken"/> so markup extensions can report
/// the correct value category and XAML IntelliSense does not suggest templates where a style is expected.
/// </remarks>
public enum TemplateToken
{
    None,
    NavigationViewItemData,
    DefaultValidation,
}
