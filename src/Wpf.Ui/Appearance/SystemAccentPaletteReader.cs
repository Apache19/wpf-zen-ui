// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Win32;

namespace Wpf.Ui.Appearance;

public static class SystemAccentPaletteReader
{
    private const string KeyPath   = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string ValueName = "AccentPalette";

    public static SystemAccentPalette? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);

        if (key?.GetValue(ValueName) is not byte[] data)
        {
            return null;
        }

        if (data.Length != 32)
        {
            return null;
        }

        // 解析 8 个 BGRA
        Color[] colors = new Color[8];

        for (int i = 0; i < 8; i++)
        {
            int offset = i * 4;

            byte b = data[offset + 0];
            byte g = data[offset + 1];
            byte r = data[offset + 2];

            colors[i] = Color.FromRgb(r, g, b);
        }

        return new SystemAccentPalette(
            colors[0], // Light3
            colors[1], // Light2
            colors[2], // Light1
            colors[3], // Accent
            colors[4], // Dark1
            colors[5], // Dark2
            colors[6], // Dark3
            colors[7]  // Alt
        );
    }
}