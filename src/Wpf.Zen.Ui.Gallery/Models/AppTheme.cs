// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Gallery.Models;

public class AppTheme
{
    public ThemePreference Preference { get; set; }

    public AccentColorSource AccentSource { get; set; } = AccentColorSource.System;

    public Uri? LitThemeResource { get; set; }

    public Uri? DarkThemeResource { get; set; }
}
