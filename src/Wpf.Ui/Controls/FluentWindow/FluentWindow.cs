// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow
/// </summary>
public partial class FluentWindow : Window
{
    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );

        OverridesDefaultStyleProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(true)
        );
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
        }
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        switch (uMsg)
        {
            // WindowChromeWorker 说lParam是-1时，不会绘制标题栏，但看起来似乎没有用处
            case PInvoke.WM_NCACTIVATE:
                var b = PInvoke.DefWindowProc(new HWND(hwnd), uMsg, new WPARAM((uint)wParam), -1);
                handled = true;
                return b;

            case PInvoke.WM_NCCREATE:

                break;

            case PInvoke.WM_NCCALCSIZE:
            {
                if (Utilities.IsOSWindows11OrNewer)
                {
                    var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

                    rcClientArea.left   += 10;
                    rcClientArea.right  -= 10;
                    rcClientArea.top    += 2;
                    rcClientArea.bottom -= 10;

                    Marshal.StructureToPtr(rcClientArea, lParam, false);


                }

                break;
            }
        }

        return new IntPtr(-1);
    }

}