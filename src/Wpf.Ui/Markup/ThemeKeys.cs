// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Markup;

public static class ThemeKeys
{
    public static ComponentResourceKey ControlCornerRadius => Create("ControlCornerRadius");

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}