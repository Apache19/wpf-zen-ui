// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Carries a native GridView header resize request to the viewport-level star coordinator.
/// </summary>
internal sealed class GridViewColumnResizeRequestedEventArgs : EventArgs
{
    internal GridViewColumnResizeRequestedEventArgs(double previousWidth, double requestedWidth)
    {
        PreviousWidth = previousWidth;
        RequestedWidth = requestedWidth;
    }

    internal double PreviousWidth { get; }

    internal double RequestedWidth { get; }

    internal bool Handled { get; set; }
}