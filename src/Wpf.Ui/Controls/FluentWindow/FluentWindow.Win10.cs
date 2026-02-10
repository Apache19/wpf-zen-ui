// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Wpf.Ui.Interop;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win10 partial
/// </summary>
public partial class FluentWindow
{
    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
        /* 此方法可以用一个渲染窗口来实现，而WPF窗口本身不适合

        // 移除 WS_SYSMENU 风格，否则会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        // 似乎移除后，右键菜单也弹不出来了。。。
        // var style = PInvoke.GetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        // style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        // PInvoke.SetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        // 必须设置窗口背景带有Alph通道，窗口背景合成效果才能生效
        hwndSource.CompositionTarget?.BackgroundColor = Colors.Transparent;

        var accentPolicy = new AccentPolicy
        {
            AccentState   = AccentState.ACCENT_ENABLE_BLURBEHIND,
            GradientColor = 0x80707070, // 背景色和边框色（ARGB格式）
            AnimationId   = 0,
            AccentFlags   = AccentFlags.EnableAcrylicBlur,
        };

        var accentPolicySize = Marshal.SizeOf(accentPolicy);
        var accentPolicyPtr  = Marshal.AllocHGlobal(accentPolicySize);
        Marshal.StructureToPtr(accentPolicy, accentPolicyPtr, false);

        try
        {
            var wca = new WindowCompositionAttributeData
            {
                Attribute  = WindowCompositionAttrib.WCA_ACCENT_POLICY,
                Data       = accentPolicyPtr,
                SizeOfData = accentPolicySize
            };

            _ = UnsafeNativeMethods.SetWindowCompositionAttribute(_hwnd, ref wca);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPolicyPtr);
        }
        */
    }
}