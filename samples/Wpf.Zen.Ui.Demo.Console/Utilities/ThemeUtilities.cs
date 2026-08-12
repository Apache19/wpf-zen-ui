// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Demo.Console.Utilities;

public static class ThemeUtilities
{
    public static void ChangeTheme()
    {
        var preference = ThemeManager.EffectiveTheme switch
        {
            ThemeVariant.Dark => ThemePreference.Light,
            _                     => ThemePreference.Dark
        };

        ThemeManager.Preference = preference;
    }
}