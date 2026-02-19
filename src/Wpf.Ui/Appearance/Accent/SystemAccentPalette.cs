// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Wpf.Ui.Appearance;

#pragma warning disable SA1516 // Elements should be separated by blank line
internal sealed record SystemAccentPalette(
    Color Light3,
    Color Light2,
    Color Light1,
    Color Accent,
    Color Dark1,
    Color Dark2,
    Color Dark3,
    Color Alt)
{
    // Win8 fallback：只有 Accent 时生成整套梯度
    public static SystemAccentPalette CreateFromAccentOnly(Color accent)
    {
        return new SystemAccentPalette(
            Lighten(accent, 0.65), // Light3
            Lighten(accent, 0.45), // Light2
            Lighten(accent, 0.25), // Light1
            accent,
            Darken(accent, 0.15), // Dark1
            Darken(accent, 0.35), // Dark2
            Darken(accent, 0.55), // Dark3
            ShiftHue(accent, 25)  // Alt
        );
    }

    private static Color Lighten(Color c, double amount)
    {
        return Color.FromRgb(L(c.R), L(c.G), L(c.B));

        byte L(byte v)
        {
            return (byte)(v + ((255 - v) * amount));
        }
    }

    private static Color Darken(Color c, double amount)
    {
        return Color.FromRgb(D(c.R), D(c.G), D(c.B));

        byte D(byte v)
        {
            return (byte)(v * (1 - amount));
        }
    }

    private static Color ShiftHue(Color c, double deg)
    {
        // 简单 HSV hue shift，用于 Alt 色
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d   = max - min;

        double h = 0;
        if (d != 0)
        {
            if (max == r)
            {
                h = ((g - b) / d) % 6;
            }
            else if (max == g)
            {
                h = ((b - r) / d) + 2;
            }
            else
            {
                h = ((r - g) / d) + 4;
            }

            h *= 60;
        }

        h = (((h + deg) % 360) + 360) % 360;
        var s = max == 0 ? 0 : d / max;
        var v = max;

        return FromHSV(h, s, v);
    }

    private static Color FromHSV(double h, double s, double v)
    {
        var i = (int)(h / 60) % 6;
        var f = (h / 60) - Math.Floor(h / 60);

        var p = v * (1 - s);
        var q = v * (1 - (f * s));
        var t = v * (1 - ((1 - f) * s));

        var (r, g, b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));
    }

    private static byte ToByte(double value)
    {
        // 先把 0..1 放大到 0..255 并四舍五入
        var i = (int)Math.Round(value * 255.0);

        // 手写 Clamp（兼容旧 .NET Framework）
        if (i > 255)
        {
            return 255;
        }

        if (i < 0)
        {
            return 0;
        }

        return (byte)i;
    }
}

#pragma warning restore SA1516 // Elements should be separated by blank line