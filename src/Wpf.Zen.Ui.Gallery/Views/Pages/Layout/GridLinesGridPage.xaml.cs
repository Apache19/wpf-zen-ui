// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Layout;

/// <summary>
/// Demonstrates independent grid-line pens and live visibility and thickness changes.
/// </summary>
[GalleryPage("Grid with customizable horizontal and vertical lines.", FluentSymbolRegular.CheckboxIndeterminate24)]
public partial class GridLinesGridPage
{
    /// <summary>
    /// Initializes the interactive grid-line examples.
    /// </summary>
    public GridLinesGridPage()
    {
        InitializeComponent();
    }
}
