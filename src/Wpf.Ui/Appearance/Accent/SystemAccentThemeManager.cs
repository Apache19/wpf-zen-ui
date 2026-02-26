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
        // todo 此处定义的顺序应该与主题里定义的资源顺序匹配
        var dic = new ResourceDictionary { [ColorKeys.SystemAccentColor] = p.Accent };

        if (mode == UIThemeMode.Light)
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Dark1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Dark2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Dark3;
        }
        else
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Light1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Light2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Light3;
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