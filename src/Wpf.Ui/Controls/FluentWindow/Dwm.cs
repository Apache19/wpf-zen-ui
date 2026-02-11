// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal sealed class Dwm
{
    public static bool SetNcRenderingPolicy(HWND hwnd, DWMNCRENDERINGPOLICY policy) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY,
            policy
        );

    public static bool SetTransitionsForceDisabled(HWND hwnd, bool disabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED,
            disabled ? 1 : 0
        );

    public static bool SetAllowNcPaint(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_ALLOW_NCPAINT,
            enabled ? 1 : 0
        );

    public static bool SetNonClientRtlLayout(HWND hWnd, bool rtlLayout) =>
        SetWindowAttribute(
            hWnd,
            DWMWINDOWATTRIBUTE.DWMWA_NONCLIENT_RTL_LAYOUT,
            rtlLayout ? 1 : 0
        );

    /// <summary>
    /// 让窗口任务栏略缩图只显示一个图标，而不是窗口内容预览
    /// </summary>
    public static bool SetForceIconicRepresentation(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_FORCE_ICONIC_REPRESENTATION,
            enabled ? 1 : 0
        );

    public static bool SetFlip3DPolicy(HWND hwnd, DWMFLIP3DWINDOWPOLICY policy) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_FLIP3D_POLICY,
            policy
        );

    public static bool SetHasIconicBitmap(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_HAS_ICONIC_BITMAP,
            enabled ? 1 : 0
        );

    // Vista 及更早版本不支持
    public static bool SetDisallowPeek(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_DISALLOW_PEEK,
            enabled ? 1 : 0
        );

    // Vista 及更早版本不支持
    public static bool SetExcludedFromPeek(HWND hwnd, bool excluded) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_EXCLUDED_FROM_PEEK,
            excluded ? 1 : 0
        );

    // 7 及更早版本不支持
    public static bool SetCloak(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
            enabled ? 1 : 0
        );

    // DWMWA_FREEZE_REPRESENTATION 7 及更早版本： 不支持此值。
    public static bool SetFreezeRepresentation(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_FREEZE_REPRESENTATION,
            enabled ? 1 : 0
        );

    // DWMWA_USE_HOSTBACKDROPBRUSH 此值从 Windows 11 Build 22000 开始受支持。
    // 此方法允许非 UWP 窗口使用宿主背景画笔。
    // 如果设置此标志，则调用Windows::UI::Composition API 的 Win32 应用可以使用宿主背景画笔构建透明效果
    // （请参阅Compositor.CreateHostBackdropBrush）。
    // pvAttribute参数指向一个BOOL类型的值。
    // 设置为TRUE表示启用窗口的宿主背景画笔，设置为FALSE表示禁用。
    public static bool SetUseHostBackdropBrush(HWND hwnd, bool enabled) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_USE_HOSTBACKDROPBRUSH,
            enabled ? 1 : 0
        );

    // DWMWA_USE_IMMERSIVE_DARK_MODE 此值从 Windows 11 Build 22000 开始受支持。
    // 当系统启用深色模式时，允许此窗口的窗口边框以深色模式颜色绘制。
    // 出于兼容性考虑，所有窗口默认都使用浅色模式，无论系统设置如何。
    // pvAttribute参数指向一个BOOL类型的值。
    // TRUE表示窗口遵循深色模式，FALSE表示始终使用浅色模式。
    public static bool UseImmersiveDarkMode(HWND hwnd, bool enabled)
    {
        int value  = enabled ? 1 : 0;

        // todo 调查旧版本 Win11 属性值不一样？
        if (!Win32.Utilities.IsOSWindows11Insider1OrNewer)
        {
            var result = SetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                value
            );

            if (result)
            {
                return true;
            }
        }

        return SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            value
        );
    }

    // DWMWA_WINDOW_CORNER_PREFERENCE 此值从 Windows 11 Build 22000 开始受支持。
    // 指定窗口的圆角首选项。
    // pvAttribute参数指向DWM_WINDOW_CORNER_PREFERENCE类型的值。
    public static bool SetWindowCornerPreference(HWND hwnd, DWM_WINDOW_CORNER_PREFERENCE cornerPreference) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
            cornerPreference
        );

    // DWMWA_BORDER_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 指定窗口边框颜色。pvAttribute参数指向一个COLORREF类型的值。应用程序负责根据状态变化（例如窗口激活状态的改变）更改边框颜色。
    // 将颜色指定为DWMWA_COLOR_NONE（值为 0xFFFFFFFE）将禁止绘制窗口边框。这样就可以创建无边框的圆角窗口。
    // 将颜色指定为DWMWA_COLOR_DEFAULT（值为 0xFFFFFFFF）会将窗口重置为使用系统默认的边框颜色。
    public static bool SetBorderColor(HWND hwnd, uint colorRef) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR,
            colorRef
        );

    // DWMWA_CAPTION_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统标题颜色的默认行为。
    public static bool SetCaptionColor(HWND hwnd, uint colorRef) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
            colorRef
        );

    // DWMWA_TEXT_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统对标题文本颜色的默认行为。
    public static bool SetTextColor(HWND hwnd, uint colorRef) =>
        SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_TEXT_COLOR,
            colorRef
        );

    // DWMWA_SYSTEMBACKDROP_TYPE DWM_SYSTEMBACKDROP_TYPE enum 此值从 Windows 11 内部版本 22621 开始受支持。
    // 检索或指定窗口的系统绘制背景材质，包括非客户区后面的背景。pvAttribute参数指向DWM_SYSTEMBACKDROP_TYPE类型的值。
    public static bool SetSystemBackdrop(HWND hwnd, DWM_SYSTEMBACKDROP_TYPE backdrop)
    {
        // todo 调查 22621 之前只能支持云母效果？

        if (backdrop != DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE)
        {
            if (Utilities.IsOSWindows11Insider2OrNewer)
            {
                
            }
        }

        return SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
            backdrop
        );
    }

    /// <summary>
    /// 据说这是个未公开的属性（DWMWA_MICA_EFFECT），用于在旧Win11(22H2之前)操作系统上启用云母效果
    /// </summary>
    public static bool SetLegacyMicaBackdrop(HWND hwnd, bool setup)
    {
        return SetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_MICA_EFFECT,
            setup ? 1 : 0
        );
    }

    private static unsafe bool SetWindowAttribute<T>(HWND hwnd, DWMWINDOWATTRIBUTE attribute, T value)
        where T : unmanaged
    {
        var result = PInvoke.DwmSetWindowAttribute(
            hwnd,
            attribute,
            &value,
            (uint)sizeof(T)
        );

        return result == HRESULT.S_OK;
    }

    /*
     在维护 WPF UI 库时，处理 Mica 和 Acrylic 的兼容性至关重要。目前 Windows 11 的 DWM 渲染 API 划分为三个主要阶段。
       以下是针对 DwmSetWindowAttribute 的适配逻辑梳理：
       1. 版本适配矩阵
       Windows 版本 (Build)   营销名称          适用属性 (Attribute)               支持的效果
       Build 22621 及以上     22H2 / 23H2       DWMWA_SYSTEMBACKDROP_TYPE (38)     Mica, Mica Alt, Acrylic
       Build 22000            21H2 (初版)       DWMWA_MICA_EFFECT (1029)           仅限 Mica
       Build 19041 - 21996    Win10 / 预览版    不支持 DWMWA 材质属性              无 (需用 SetWindowCompositionAttribute)
       2. 关键代码逻辑实现
       为了保证 UI 库的健壮性，建议在设置材质时采用 “自上而下” 的降级策略：
       A. 新版方案 (Build 22621+)
       使用 DWMWA_SYSTEMBACKDROP_TYPE (Microsoft Learn)，这是官方推荐的标准做法。
       csharp
       // 对应 Attribute ID: 38
       public enum DWM_SYSTEMBACKDROP_TYPE : uint
       {
           DWMSBT_AUTO = 0,
           DWMSBT_NONE = 1,
           DWMSBT_MAINWINDOW = 2,      // Mica
           DWMSBT_TRANSIENTWINDOW = 3, // Acrylic
           DWMSBT_TABBEDWINDOW = 4     // Mica Alt
       }
       Use code with caution.

       B. 旧版 Win11 方案 (Build 22000)
       该版本下属性 38 无效，必须通过硬编码 1029 来开启 Mica。
       csharp
       // 对应 Attribute ID: 1029
       // 注意：此版本参数是 BOOL 并非枚举
       int trueValue = 0x01;
       DwmSetWindowAttribute(hwnd, 1029, ref trueValue, sizeof(uint));
       Use code with caution.

       3. 维护建议与坑点
       背景透明度 (关键)：
       在 WPF 中，必须确保 Window.Background 设置为 Transparent，且 AllowsTransparency 的处理需谨慎（通常建议设为 false 并通过 HwndSource 修改 WS_EX_LAYERED 或使用 WindowChrome），否则云母材质会被 WPF 的默认白色背景遮挡。
       Dark Mode 适配：
       Mica 效果会根据系统主题自动深浅变化。在 22000 版本中，如果你开启了 1029，通常还需要配合 DWMWA_USE_IMMERSIVE_DARK_MODE (20) 来确保标题栏文字颜色正确。
       Acrylic 局限性：
       在 22000 版本中，通过 DWM 直接开启窗口级别 Acrylic 是不存在官方路径的。如果你的库承诺提供 Acrylic，在 22000 上可能需要回退到 SetWindowCompositionAttribute (非公开 API) 方案。
       性能检测：
       可以通过 DwmIsCompositionEnabled 先判断 DWM 是否开启，避免在低配机器或远程桌面上产生渲染异常。
       你需要一份完整的 C# 封装类代码来自动处理这些版本判断吗？

       DwmIsCompositionEnabled 官方文档说Win8以上系统是无法关闭的，还需要检查？
       你是对的。根据 Microsoft 官方文档，自 Windows 8 起，桌面窗口管理器 (DWM) 已经无法被软件或用户关闭。该 API 现在的返回值永远为 TRUE。
       但在维护 UI 库时，出于极端兼容性和性能考虑，仍有一个特殊场景需要你通过其他方式进行逻辑检查：
       1. 为什么不再依赖 DwmIsCompositionEnabled
       虽然 DWM 无法关闭，但 DwmIsCompositionEnabled 已经失去了防御性编程的意义。即便它返回 TRUE，也不代表云母（Mica）能正常渲染。
       2. 真正需要检查的“深坑”：远程桌面 (RDP)
       这是 UI 库最容易崩溃或出现黑屏的地方。当用户通过 远程桌面 访问 Windows 11 时：
       DwmIsCompositionEnabled 依然返回 TRUE。
       但由于带宽限制，云母 (Mica) 和亚克力 (Acrylic) 会失效，窗口背景可能变成纯色或出现渲染伪像。
       建议做法：通过 SystemParameters.IsRemoteSession 判断。如果是远程会话，建议 UI 库自动回退到纯色背景，以避免视觉错误和性能浪费。
       3. 给 UI 库维护者的最佳实践建议
       既然 DWM 永远开启，你应该将精力集中在版本分支逻辑上。以下是推荐的底层检测逻辑：
       csharp
       public void ApplyMica(IntPtr hwnd)
       {
           // 1. 过滤不支持的系统 (Win10 及以下)
           if (Environment.OSVersion.Version.Build < 22000) return;
       
           // 2. 检查远程桌面环境 (性能/视觉优化)
           if (SystemParameters.IsRemoteSession) return; 
       
           // 3. 分版本调用
           if (Environment.OSVersion.Version.Build >= 22621) 
           {
               // 使用新版 API (Attribute 38)
               SetBackdropType(hwnd, DWMSBT_MAINWINDOW);
           }
           else 
           {
               // 使用旧版 API (Attribute 1029)
               SetMicaOld(hwnd, true);
           }
       }
       Use code with caution.
       
       4. 补充一个属性：DWMWA_USE_IMMERSIVE_DARK_MODE
       在 Windows 11 上，无论新旧版，Mica 都需要窗口意识到自己处于深色模式才能正确显示。
       属性 ID：20
       注意：在 Build 22000 之前，这个 ID 是 19，但在 Win11 稳定版中，20 是标准用法。
       总结：不再需要检查 DWM 是否开启，但必须检查 远程桌面环境 和 Windows Build 版本号。
     */
}