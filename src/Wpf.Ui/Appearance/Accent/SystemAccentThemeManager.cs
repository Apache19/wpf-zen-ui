// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Theming;

namespace Wpf.Ui.Appearance.Accent;

internal static class SystemAccentThemeManager
{
    private static ResourceDictionary? _accentDictionary;
    private static bool                _isEnabled;

    private static void Apply(Application app, UIThemeMode mode)
    {
        var palette = SystemAccentPaletteReader.Read();
        if (palette == null)
        {
            return;
        }

        var dict = BuildDictionary(palette, mode);
        ReplaceAccentDictionary(app, dict);

        _isEnabled = true;
    }

    internal static void Disable(Application app)
    {
        if (!_isEnabled)
        {
            return;
        }

        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
            _accentDictionary = null;
        }

        _isEnabled = false;
    }

    internal static void Update(Application app, bool useSystemAccent, UIThemeMode mode)
    {
        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
            _accentDictionary = null;
        }

        if (useSystemAccent)
        {
            Apply(app, mode);
        }
        else
        {
            Disable(app);
        }
    }

    private static ResourceDictionary BuildDictionary(SystemAccentPalette p, UIThemeMode mode)
    {
        var dic = new ResourceDictionary { [ColorKeys.SystemAccentColor] = p.Accent };

        if (mode == UIThemeMode.Light)
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

    private static void ReplaceAccentDictionary(Application app, ResourceDictionary newDict)
    {
        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
        }

        _accentDictionary = newDict;
        app.Resources.MergedDictionaries.Add(_accentDictionary);
    }
}