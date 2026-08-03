// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Stores native frame measurements in physical pixels.
/// </summary>
/// <remarks>
/// WPF layout units must never enter non-client hit testing. Keeping this value in device pixels avoids the
/// rounding errors that made WindowChrome resize borders unreliable on mixed-DPI desktops.
/// </remarks>
internal readonly struct WindowFrameInsets
{
    public WindowFrameInsets(int left, int top, int right, int bottom)
    {
        Left = Math.Max(0, left);
        Top = Math.Max(0, top);
        Right = Math.Max(0, right);
        Bottom = Math.Max(0, bottom);
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }
}
