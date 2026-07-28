// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for templates published by WPF UI.
/// </summary>
/// <remarks>
/// These keys remain public for programmatic resource lookup. XAML consumers should normally use
/// <c>DynamicTemplate</c> or <c>StaticTemplate</c>, whose enum argument provides token completion.
/// </remarks>
public static class TemplateKeys
{
    public static ComponentResourceKey NavigationViewItemDataTemplate { get; } = Create(nameof(NavigationViewItemDataTemplate));
    public static ComponentResourceKey DefaultValidationTemplate      { get; } = Create(nameof(DefaultValidationTemplate));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}