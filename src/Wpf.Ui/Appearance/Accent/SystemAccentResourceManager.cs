// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Theming;

namespace Wpf.Ui.Appearance.Accent;

// todo 考虑高对比度
//       当系统处于高对比度模式时，

internal static class SystemAccentResourceManager
{
    private static ResourceDictionary? _accentDictionary;

    internal static void Apply(ResourceDictionary resource, UIThemeMode mode)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        var palette = SystemAccentPaletteReader.Read();
        if (palette == null)
        {
            return;
        }

        Remove(resource);

        var dict = BuildDictionary(palette, mode);
        _accentDictionary = dict;

        resource.MergedDictionaries.Add(dict);
    }

    internal static void Remove(ResourceDictionary resource)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (_accentDictionary != null)
        {
            resource.MergedDictionaries.Remove(_accentDictionary);
            _accentDictionary = null;
        }
    }

    internal static void SetSystemAccentEnabled(ResourceDictionary resource, bool enabled, UIThemeMode mode)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        Remove(resource);

        if (enabled)
        {
            Apply(resource, mode);
        }
    }

    private static ResourceDictionary BuildDictionary(SystemAccentPalette p, UIThemeMode mode)
    {
        var dic = new ResourceDictionary { [ColorKeys.SystemAccentColor] = p.Accent };

        if (SystemParameters.HighContrast)
        {
            
        }
        else if (mode == UIThemeMode.Light)
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Dark1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Dark2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Dark3;

            dic[BrushKeys.SystemAccentColorBrush]                     = new SolidColorBrush(p.Accent);
            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = new SolidColorBrush(p.Dark2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = new SolidColorBrush(p.Dark3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = new SolidColorBrush(p.Dark2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = new SolidColorBrush(p.Dark3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = new SolidColorBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = new SolidColorBrush(p.Dark1) { Opacity = 0.9 };
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = new SolidColorBrush(p.Dark1) { Opacity = 0.8 };
            dic[BrushKeys.SystemFillColorAttentionBrush]              = new SolidColorBrush(p.Accent);
        }
        else
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Light1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Light2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Light3;

            dic[BrushKeys.SystemAccentColorBrush]                     = new SolidColorBrush(p.Accent);
            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = new SolidColorBrush(p.Light1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = new SolidColorBrush(p.Light2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = new SolidColorBrush(p.Light3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = new SolidColorBrush(p.Light1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = new SolidColorBrush(p.Light2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = new SolidColorBrush(p.Light3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = new SolidColorBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = new SolidColorBrush(p.Light2);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = new SolidColorBrush(p.Light2) { Opacity = 0.9 };
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = new SolidColorBrush(p.Light2) { Opacity = 0.8 };
            dic[BrushKeys.SystemFillColorAttentionBrush]              = new SolidColorBrush(p.Accent);
        }

        return dic;
    }
}
