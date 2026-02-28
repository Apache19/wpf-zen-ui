// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Win32;

namespace Wpf.Ui.Appearance;

internal static class SystemAccentPaletteReader
{
    private const string AccentKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string DwmKey    = @"SOFTWARE\Microsoft\Windows\DWM";

    public static SystemAccentPalette? Read(Color? fallbackAccent = null)
    {
        // todo 根据系统版本选择不同方案

        if (fallbackAccent.HasValue)
        {
            return SystemAccentPalette.CreateFromAccentOnly(fallbackAccent.Value);
        }

        using (var key = Registry.CurrentUser.OpenSubKey(AccentKey))
        {
            if (key?.GetValue("AccentPalette") is byte[] { Length: >= 32 } bytes)
            {
                return ParsePalette(bytes);
            }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(DwmKey))
        {
            if (key?.GetValue("AccentColor") is int raw)
            {
                var color = FromRawABGR(raw);
                return SystemAccentPalette.CreateFromAccentOnly(color);
            }
        }

        return null;
    }

    private static SystemAccentPalette ParsePalette(byte[] data)
    {
        return new SystemAccentPalette(
            B(0), B(4), B(8), B(12), B(16), B(20), B(24), B(28)
        );

        Color B(int i) => Color.FromRgb(data[i], data[i + 1], data[i + 2]);
    }

    // todo: 考察 RGB byte 顺序
    private static Color FromRawABGR(int value)
    {
        byte r = (byte)(value & 0xFF);
        byte g = (byte)((value >> 8) & 0xFF);
        byte b = (byte)((value >> 16) & 0xFF);
        return Color.FromRgb(r, g, b);
    }
}