// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Wpf.Zen.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Implements the pre-Windows 11 DWM shadow bridge for the FluentWindow non-client strategy.
/// </summary>
public partial class FluentWindow
{
    /// <summary>
    /// Maintains the minimal DWM client-frame extension used by fully client-rendered frames before Windows 11.
    /// </summary>
    /// <remarks>
    /// Handling WM_NCCALCSIZE removes every native frame pixel in <see cref="FluentWindowFrameStrategy.ClientBorder"/>.
    /// On Vista through Windows 10, DWM consequently stops producing the external window shadow. Extending one
    /// physical pixel on every edge keeps the DWM surface alive without exposing a visible glass band; the WPF
    /// template continues to own the actual one-pixel border. Windows 11 uses an equivalent DWM extension for its
    /// own outline and rounded corners, but manages it together with the system backdrop because an active backdrop
    /// requires different margins.
    /// </remarks>
    private void UpdateLegacyFrameExtension()
    {
        if (Utilities.IsWin_11_Or_Greater || _hwnd == HWND.Null || !PInvoke.IsWindow(_hwnd))
        {
            return;
        }

        var extend =
            GetFrameStrategy() == FluentWindowFrameStrategy.ClientBorder && !SystemParameters.HighContrast;
        var margins = extend
            ? new MARGINS
            {
                cxLeftWidth = 1,
                cxRightWidth = 1,
                cyTopHeight = 1,
                cyBottomHeight = 1,
            }
            : default;

        _ = PInvoke.DwmExtendFrameIntoClientArea(_hwnd, in margins);
    }
}
