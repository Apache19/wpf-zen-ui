// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* This Source Code is partially based on the source code provided by the .NET Foundation. */

namespace Wpf.Zen.Ui.Hardware;

/// <summary>
/// Stores DPI information from which a <see cref="System.Windows.Media.Visual"/> or <see cref="System.Windows.UIElement"/>
/// is rendered.
/// </summary>
public readonly struct DisplayDpi
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayDpi"/> structure.
    /// </summary>
    /// <param name="dpiScaleX">The DPI scale on the X axis.</param>
    /// <param name="dpiScaleY">The DPI scale on the Y axis.</param>
    public DisplayDpi(double dpiScaleX, double dpiScaleY)
    {
        DpiScaleX = dpiScaleX;
        DpiScaleY = dpiScaleY;

        DpiX = (int)Math.Round(DpiHelper.DefaultDpi * dpiScaleX, MidpointRounding.AwayFromZero);
        DpiY = (int)Math.Round(DpiHelper.DefaultDpi * dpiScaleY, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayDpi"/> structure.
    /// </summary>
    /// <param name="dpiX">The DPI on the X axis.</param>
    /// <param name="dpiY">The DPI on the Y axis.</param>
    public DisplayDpi(int dpiX, int dpiY)
    {
        DpiX = dpiX;
        DpiY = dpiY;

        DpiScaleX = dpiX / (double)DpiHelper.DefaultDpi;
        DpiScaleY = dpiY / (double)DpiHelper.DefaultDpi;
    }

    /// <summary>
    /// Gets the DPI on the X axis.
    /// </summary>
    public int DpiX { get; }

    /// <summary>
    /// Gets the DPI on the Y axis.
    /// </summary>
    public int DpiY { get; }

    /// <summary>
    /// Gets the DPI scale on the X axis.
    /// </summary>
    public double DpiScaleX { get; }

    /// <summary>
    /// Gets the DPI scale on the Y axis.
    /// </summary>
    public double DpiScaleY { get; }

    /// <summary>
    /// Returns a string representation of the display DPI information.
    /// </summary>
    /// <returns>A formatted string containing the DPI values and scale factors.</returns>
    public override string ToString()
    {
        return $"DisplayDpi (DpiX: {DpiX}, DpiY: {DpiY}, ScaleX: {DpiScaleX:F2}, ScaleY: {DpiScaleY:F2})";
    }
}
