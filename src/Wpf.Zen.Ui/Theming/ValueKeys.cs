// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for non-color theme values defined in <c>Variables.xaml</c>.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicValue</c> or <c>StaticValue</c>. This key surface remains
/// available for resource dictionary declarations and programmatic lookup.
/// </remarks>
public static class ValueKeys
{
    public static ComponentResourceKey DefaultControlMinWidth                   { get; } = Create(nameof(DefaultControlMinWidth));
    public static ComponentResourceKey DefaultControlMinHeight                  { get; } = Create(nameof(DefaultControlMinHeight));

    public static ComponentResourceKey DefaultIconFontSize                      { get; } = Create(nameof(DefaultIconFontSize));
    public static ComponentResourceKey DefaultThemeFontSize                    { get; } = Create(nameof(DefaultThemeFontSize));
    public static ComponentResourceKey ControlCornerRadius                      { get; } = Create(nameof(ControlCornerRadius));
    public static ComponentResourceKey OverlayCornerRadius                      { get; } = Create(nameof(OverlayCornerRadius));
    public static ComponentResourceKey PopupCornerRadius                        { get; } = Create(nameof(PopupCornerRadius));
    public static ComponentResourceKey TextControlThemePadding                  { get; } = Create(nameof(TextControlThemePadding));
    public static ComponentResourceKey ButtonThemePadding                       { get; } = Create(nameof(ButtonThemePadding));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
