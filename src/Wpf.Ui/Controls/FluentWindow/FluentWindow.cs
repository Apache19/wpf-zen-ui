// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Interop;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A custom WinUI Window with more convenience methods.
/// </summary>
public class FluentWindow : Window
{
    private DwmWindowAttributes  _dwmWindowAttributes = new DwmWindowAttributes();

    public FluentWindow()
    {
    }

    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );

        OverridesDefaultStyleProperty.OverrideMetadata(typeof(FluentWindow), new FrameworkPropertyMetadata(true));
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        // OnCornerPreferenceChanged(default, WindowCornerPreference);
        // OnExtendsContentIntoTitleBarChanged(false, ExtendsContentIntoTitleBar);
        // OnBackdropTypeChanged(default, WindowBackdropType);

        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
            // _dwmWindowAttributes.SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW).UseHostBackdropBrush(true);

            var r = UnsafeNativeMethods.RemoveWindowCaption(hwndSource.Handle);
            Debug.WriteLine($"RemoveWindowCaption result: {r}");

            r = UnsafeNativeMethods.ApplyWindowDarkMode(hwndSource.Handle);
            Debug.WriteLine($"ApplyWindowDarkMode result: {r}");

            r = WindowBackdrop.ApplyBackdrop(hwndSource.Handle, WindowBackdropType.Acrylic);
            Debug.WriteLine($"ApplyBackdrop result: {r}");

            unsafe
            {
                // DWMNCRENDERINGPOLICY ncrp = DWMNCRENDERINGPOLICY.DWMNCRP_DISABLED;
                // PInvoke.DwmSetWindowAttribute((HWND)hwndSource.Handle, DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, &ncrp, sizeof(DWMNCRENDERINGPOLICY));
                //
                // bool b = false;
                // PInvoke.DwmSetWindowAttribute((HWND)hwndSource.Handle, DWMWINDOWATTRIBUTE.DWMWA_ALLOW_NCPAINT, &b, sizeof(bool));

                // PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, new MARGINS() { cxLeftWidth = 25, cxRightWidth = 25, cyTopHeight = 25, cyBottomHeight = 25 });
            }
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
                if (wParam.ToInt32() != 0)
                {
                    var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

                    // PInvoke.CopyRect(out p.rgrc._2, in p.rgrc._1);
                    // PInvoke.CopyRect(out p.rgrc._1, in p.rgrc._0);
                    // PInvoke.CopyRect(out p.rgrc._0, in p.rgrc._1);

                    p.rgrc._0.left   += 13;
                    p.rgrc._0.right  -= 13;
                    p.rgrc._0.top    += 2;
                    p.rgrc._0.bottom -= 13;

                    // p.rgrc._1.left   += 10;
                    // p.rgrc._1.right  -= 10;
                    // p.rgrc._1.top    += 2;
                    // p.rgrc._1.bottom -= 8;

                    // p.rgrc._2.left   += 10;
                    // p.rgrc._2.right  -= 10;
                    // p.rgrc._2.top    += 0;
                    // p.rgrc._2.bottom -= 10;

                    Marshal.StructureToPtr(p, lParam, false);
                    handled = true;
                    return new IntPtr(PInvoke.WVR_REDRAW | PInvoke.WVR_ALIGNTOP | PInvoke.WVR_ALIGNBOTTOM);
                }

                // else
                // {
                //     var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);
                //
                //     rcClientArea.left   += 10;
                //     rcClientArea.right  -= 10;
                //     rcClientArea.top    += 2;
                //     rcClientArea.bottom -= 10;
                //
                //     Marshal.StructureToPtr(rcClientArea, lParam, false);
                // }

                break;
            }
        }

        return new IntPtr(-1);
    }
}