// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

public readonly record struct SystemAccentPalette(
    Color Light3,
    Color Light2,
    Color Light1,
    Color Accent,
    Color Dark1,
    Color Dark2,
    Color Dark3,
    Color Alt
);