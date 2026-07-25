// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes how <see cref="FluentWindow"/> divides its HWND between the native frame and WPF content.
/// </summary>
/// <remarks>
/// This type is intentionally independent of visual styling. It describes only the physical client rectangle
/// returned from <c>WM_NCCALCSIZE</c>; the matching WPF border is selected separately by the window template.
/// </remarks>
internal enum FluentWindowFrameStrategy
{
    Windows11Native,
    Windows11Client,
    Windows10Hybrid,
    ClientBorder,
}
