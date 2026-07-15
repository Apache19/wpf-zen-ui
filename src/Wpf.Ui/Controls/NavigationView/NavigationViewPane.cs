// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Hosts a <see cref="NavigationView"/> pane and reports its rendered width to the owning control.
/// </summary>
internal sealed class NavigationViewPane : Grid
{
    /// <inheritdoc />
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (sizeInfo.WidthChanged && TemplatedParent is NavigationView navigationView)
        {
            navigationView.UpdatePaneActualWidth(ActualWidth);
        }
    }
}
