// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;

#pragma warning disable SA1027 // Use tabs correctly
#pragma warning disable SA1028
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win10 partial
/// </summary>
public partial class FluentWindow
{
    private HWND _hwnd;

    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
        _hwnd = new HWND(hwndSource.Handle);
        //FixWhiteFrame(_hwnd);

        // 移除 WS_SYSMENU 风格，否则会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        // 似乎移除后，右键菜单也弹不出来了。。。
        // var style = PInvoke.GetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        // style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        // PInvoke.SetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        // 必须设置窗口背景为透明色，窗口背景合成效果才能生效
        hwndSource.CompositionTarget?.BackgroundColor = Colors.Transparent;
    }

    // https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/dwm/customframe.md

    private IntPtr OnWmActive_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd = (HWND)hwndIntPtr;

        PInvoke.DwmDefWindowProc(hwnd, PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED, new WPARAM(0xFFFF5500), 0, out LRESULT result);

        // 扩展顶部1逻辑像素可以消除启用透明合成时的顶部白色横线
        // 但不能扩展过多，否则扩展超出合并边框时又会变成白色
        var margins = new MARGINS()
        {
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };


        _ = PInvoke.DwmExtendFrameIntoClientArea(hwnd, in margins);

        handled = false;
        return IntPtr.Zero;
    }

    private IntPtr OnWmNcCalcSize_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        /*var hwnd = (HWND)hwndIntPtr;
        var rect = Marshal.PtrToStructure<RECT>(lParam);

        var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

        // if (isMaximized)
        {
            var (cx, cy) = GetWindowBorderThickness(hwnd);

            rect.left   += cx;
            rect.right  -= cx;
            rect.top    += 2;
            rect.bottom -= cy;
        }
        else
        {
            EnsureWindowDpi(hwnd);

            var dpi = _cachedDpi;

            var cx = (int)(1 * dpi.DpiScaleX);
            var cy = (int)(1 * dpi.DpiScaleY);

            rect.left   += cx;
            rect.right  -= cx;
            rect.bottom -= cy;
        }

        Marshal.StructureToPtr(rect, lParam, false);*/

        handled = false;
        return IntPtr.Zero;
    }

    // 1. 定义结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct WTA_OPTIONS
    {
        public uint Flags; // 标志位
        public uint Mask;  // 掩码
    }

    // 定义常量
    const uint WTNCA_NODRAWCAPTION = 0x00000001; // 不绘制标题
    const uint WTNCA_NODRAWICON    = 0x00000002; // 不绘制图标
    const uint WTNCA_NOSYSMENU     = 0x00000004; // 不绘制系统菜单
    const uint WTNCA_NOMIRRORHELP  = 0x00000008; // 不镜像帮助按钮

    // 2. 调用方法
    public void FixWhiteFrame(IntPtr hwnd)
    {
        WTA_OPTIONS options = new()
        {
            // 这里的策略是：通过掩码控制 DWM 重新计算 NC 区域的渲染属性
            Flags = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON,
            Mask  = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON
        };

        WindowCompositionAttributeData data = new()
        {
            Attribute = WindowCompositionAttrib.WCA_THEME_ATTRIBUTES // WCA_THEME_ATTRIBUTES
        };

        IntPtr pOptions = Marshal.AllocHGlobal(Marshal.SizeOf(options));
        Marshal.StructureToPtr(options, pOptions, false);

        data.Data       = pOptions;
        data.SizeOfData = Marshal.SizeOf(options);

        UnsafeNativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
        Marshal.FreeHGlobal(pOptions);
    }

    private void OnLoaded_Win10111(object sender, RoutedEventArgs e)
    {
        //var themeData = new ThemeAttributeData
        //{
        //    Flags          = ThemeAttributeFlags.EnableAcrylic | ThemeAttributeFlags.UseImmersiveDarkMode, // 启用亚克力
        //    ColorAttribute = 0x80F07070,                                                                   // ARGB: 透明度 + RGB
        //    AnimationId    = 0,
        //    Reserved       = 0
        //};

        //var themeDataSize = Marshal.SizeOf(themeData);
        //var themeDataPtr  = Marshal.AllocHGlobal(themeDataSize);
        //Marshal.StructureToPtr(themeData, themeDataPtr, false);

        //try
        //{
        //    var wca = new WindowCompositionAttributeData
        //    {
        //        Attribute  = WindowCompositionAttrib.WCA_THEME_ATTRIBUTES,
        //        Data       = themeDataPtr,
        //        SizeOfData = themeDataSize
        //    };

        //    var ret   = UnsafeNativeMethods.SetWindowCompositionAttribute(_hwnd, ref wca);
        //    int error = Marshal.GetLastWin32Error();
        //    Debug.WriteLine($"SetWindowCompositionAttribute return: {ret}, error code: {error}");
        //}
        //finally
        //{
        //    Marshal.FreeHGlobal(themeDataPtr);
        //}
    }

    private void OnLoaded_Win10(object sender, RoutedEventArgs e)
    {
        var accentPolicy = new AccentPolicy
        {
            AccentState   = AccentState.ACCENT_ENABLE_BLURBEHIND,
            GradientColor = 0x80707070, // 背景色和边框色（ARGB格式）
            AnimationId   = 0,
            // 启用亚克力背景色
            AccentFlags = AccentFlags.EnableAcrylicBlur,
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

        var themeData = new ThemeAttributeData
        {
            Flags          = ThemeAttributeFlags.EnableAcrylic | ThemeAttributeFlags.UseAccentColor, // 启用亚克力
            ColorAttribute = 0x80707070,                                                             // ARGB: 透明度 + RGB
            AnimationId    = 0,
            Reserved       = 0
        };

        var themeDataSize = Marshal.SizeOf(themeData);
        var themeDataPtr  = Marshal.AllocHGlobal(themeDataSize);
        Marshal.StructureToPtr(themeData, themeDataPtr, false);

        try
        {
            var wca = new WindowCompositionAttributeData
            {
                Attribute  = WindowCompositionAttrib.WCA_THEME_ATTRIBUTES,
                Data       = themeDataPtr,
                SizeOfData = themeDataSize
            };

            // _ = UnsafeNativeMethods.SetWindowCompositionAttribute(_hwnd, ref wca);
        }
        finally
        {
            Marshal.FreeHGlobal(themeDataPtr);
        }
    }
}

// gptc 使用 WCA_ACCENT_POLICY 会强行绘制主题色，我打算改用 WCA_THEME_ATTRIBUTES，请问 WCA_THEME_ATTRIBUTES 相关的定义和使用方法？
// 		`WCA_THEME_ATTRIBUTES` 是 Windows 10 1809+ 引入的窗口组合属性，用于控制窗口的主题属性，包括亚克力背景。相比 `WCA_ACCENT_POLICY`，它提供更精细的控制，不会强制应用主题色。
// 		
// 		## 相关定义（P/Invoke）
// 		
// 		```csharp
// 		// 在 WindowCompositionAttrib 枚举中添加
// 		internal enum WindowCompositionAttrib
// 		{
// 		    // ... 其他值
// 		    WCA_THEME_ATTRIBUTES = 19
// 		}
// 		
// 		// 主题属性标志
// 		[Flags]
// 		internal enum ThemeAttributeFlags : uint
// 		{
// 		    None = 0x0,
// 		    UseImmersiveDarkMode = 0x1,
// 		    UseAccentColor = 0x2,
// 		    EnableAcrylic = 0x4,
// 		    EnableMica = 0x8,
// 		    EnableTabbed = 0x10
// 		}
// 		
// 		// 主题属性结构
// 		[StructLayout(LayoutKind.Sequential)]
// 		internal struct ThemeAttributeData
// 		{
// 		    public ThemeAttributeFlags Flags;
// 		    public uint ColorAttribute;  // ARGB 格式的颜色
// 		    public uint AnimationId;
// 		    public uint Reserved;        // 必须为 0
// 		}
// 		```
// 		
// 		## 使用方法
// 		
// 		替换 `OnLoaded_Win10` 方法中的代码：
// 		
// 		```csharp
// 		private void OnLoaded_Win10(object sender, RoutedEventArgs e)
// 		{
// 		    var themeData = new ThemeAttributeData
// 		    {
// 		        Flags = ThemeAttributeFlags.EnableAcrylic,  // 启用亚克力
// 		        ColorAttribute = 0x80F07070,                // ARGB: 透明度 + RGB
// 		        AnimationId = 0,
// 		        Reserved = 0
// 		    };
// 		
// 		    var themeDataSize = Marshal.SizeOf(themeData);
// 		    var themeDataPtr = Marshal.AllocHGlobal(themeDataSize);
// 		    Marshal.StructureToPtr(themeData, themeDataPtr, false);
// 		
// 		    try
// 		    {
// 		        var wca = new WindowCompositionAttributeData
// 		        {
// 		            Attribute = WindowCompositionAttrib.WCA_THEME_ATTRIBUTES,
// 		            Data = themeDataPtr,
// 		            SizeOfData = themeDataSize
// 		        };
// 		
// 		        _ = UnsafeNativeMethods.SetWindowCompositionAttribute(_hwnd, ref wca);
// 		    }
// 		    finally
// 		    {
// 		        Marshal.FreeHGlobal(themeDataPtr);
// 		    }
// 		}
// 		```
// 		
// 		## 关键区别
// 		
// 		1. **颜色控制**：`WCA_THEME_ATTRIBUTES` 的 `ColorAttribute` 只影响亚克力底色，不会强制改变窗口边框/标题栏颜色
// 		2. **标志位**：通过 `Flags` 字段可以组合多个效果：
// 		   - `EnableAcrylic`: 亚克力模糊
// 		   - `EnableMica`: Mica 材质（Win11）
// 		   - `UseImmersiveDarkMode`: 深色模式
// 		   - `UseAccentColor`: 使用系统强调色（可选）
// 		3. **系统版本**：需要 Windows 10 1809 (17763) 或更高版本
// 		
// 		## 建议
// 		
// 		1. **版本检测**：添加系统版本检查，低版本回退到 `WCA_ACCENT_POLICY`
// 		2. **动态更新**：可以在运行时修改主题属性，响应系统主题变化
// 		3. **错误处理**：`SetWindowCompositionAttribute` 可能失败，建议检查返回值
// 		
// 		需要我提供完整的版本兼容性实现示例吗？
// gptc 