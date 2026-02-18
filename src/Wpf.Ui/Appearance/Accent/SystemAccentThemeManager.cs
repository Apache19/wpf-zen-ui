// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;

namespace Wpf.Ui.Appearance.Accent;

internal static class SystemAccentThemeManager
{
    private static ResourceDictionary? _accentDictionary;
    private static bool                _isEnabled;

    public static void Apply(ThemeMode mode)
    {
        var palette = SystemAccentPaletteReader.Read();
        if (palette == null)
        {
            return;
        }

        var dict = BuildDictionary(palette, mode);
        ReplaceAccentDictionary(dict);

        _isEnabled = true;
    }

    public static void Disable()
    {
        if (!_isEnabled)
        {
            return;
        }

        var app = EnsureApplication();

        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
            _accentDictionary = null;
        }

        _isEnabled = false;
    }

    public static void Update(bool useSystemAccent, ThemeMode mode)
    {
        var app = EnsureApplication();

        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
            _accentDictionary = null;
        }

        if (useSystemAccent)
        {
            Apply(mode);
        }
        else
        {
            Disable();
        }
    }

    private static ResourceDictionary BuildDictionary(SystemAccentPalette p, ThemeMode mode)
    {
        var dic = new ResourceDictionary
        {
            [AccentResourceKeys.Accent] = p.Accent
        };

        if (mode == ThemeMode.Light)
        {
            dic[AccentResourceKeys.AccentPrimary]   = p.Dark1;
            dic[AccentResourceKeys.AccentSecondary] = p.Dark2;
            dic[AccentResourceKeys.AccentTertiary]  = p.Dark3;
        }
        else
        {
            dic[AccentResourceKeys.AccentPrimary]   = p.Light1;
            dic[AccentResourceKeys.AccentSecondary] = p.Light2;
            dic[AccentResourceKeys.AccentTertiary]  = p.Light3;
        }

        return dic;
    }

    private static void ReplaceAccentDictionary(ResourceDictionary newDict)
    {
        var app = EnsureApplication();

        if (_accentDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_accentDictionary);
        }

        _accentDictionary = newDict;
        app.Resources.MergedDictionaries.Add(_accentDictionary);
    }

    private static Application EnsureApplication()
    {
        return Application.Current ?? new Application();
    }
}