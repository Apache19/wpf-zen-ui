// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines how scroll bars are laid out relative to the content area in a <see cref="System.Windows.Controls.ScrollViewer"/>.
/// </summary>
public enum ScrollViewerOverlayMode
{
    /// <summary>
    /// Scroll bars are overlaid on top of the content area, maximizing the viewport at the cost of
    /// potentially obscuring content. This is the modern default.
    /// </summary>
    Overlay,

    /// <summary>
    /// Scroll bars are placed outside the content area, guaranteeing that no content is ever obscured
    /// by scroll bars.
    /// </summary>
    Traditional,
}
