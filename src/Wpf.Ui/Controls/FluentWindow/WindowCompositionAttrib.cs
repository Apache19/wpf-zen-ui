// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Ui.Controls;

#pragma warning disable SA1649 // File name should match first type name

// 这是未公开的API
internal enum WindowCompositionAttrib
{
    WCA_UNDEFINED                     = 0,  // - 未定义
    WCA_NCRENDERING_ENABLED           = 1,  // - 启用或禁用窗口非客户区（标题栏、边框）的渲染
    WCA_NCRENDERING_POLICY            = 2,  // - 控制非客户区的渲染方式（如是否启用透明效果）
    WCA_TRANSITIONS_FORCEDISABLED     = 3,  // - 禁用窗口打开、关闭、最小化等过渡动画
    WCA_ALLOW_NCPAINT                 = 4,  // - 允许应用程序在非客户区进行自定义绘制。
    WCA_CAPTION_BUTTON_BOUNDS         = 5,  // - 窗口按钮区域，设置还是读取？
    WCA_NONCLIENT_RTL_LAYOUT          = 6,  // - 用于支持从右到左（RTL）语言的窗口布局
    WCA_FORCE_ICONIC_REPRESENTATION   = 7,  // - 强制窗口以图标形式显示，通常用于任务栏预览或 Alt+Tab 切换窗口时的显示效果。
    WCA_EXTENDED_FRAME_BOUNDS         = 8,  // - 获取窗口扩展边框的边界，用于获取窗口的实际边框范围（包括阴影等效果）。
    WCA_HAS_ICONIC_BITMAP             = 9,  // - 窗口是否具有图标化位图，用于判断窗口是否有用于任务栏预览的图标化位图。
    WCA_THEME_ATTRIBUTES              = 10, // - 设置窗口的主题属性，控制窗口的主题样式（如颜色、字体等）。需要 Windows 10 1809 (17763) 或更高版本
    WCA_NCRENDERING_EXILED            = 11, // - 将非客户区渲染排除在外，用于某些特殊场景下禁用非客户区的渲染。
    WCA_NCADORNMENTINFO               = 12, // - 获取非客户区的装饰信息，用于获取窗口边框、标题栏等装饰信息。
    WCA_EXCLUDED_FROM_LIVEPREVIEW     = 13, // - 将窗口从实时预览中排除，禁用窗口在任务栏预览或 Alt+Tab 中的实时预览效果。
    WCA_VIDEO_OVERLAY_ACTIVE          = 14,
    WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 15, // - 强制窗口显示为活动状态，即使窗口未激活，也显示为活动状态的外观。
    WCA_DISALLOW_PEEK                 = 16, // - 禁用窗口的 Peek 预览，禁用鼠标悬停在任务栏时窗口的预览效果。
    WCA_CLOAK                         = 17, // - 隐藏窗口内容，用于临时隐藏窗口内容（如切换虚拟桌面时）。
    WCA_CLOAKED                       = 18, // - 判断窗口是否被隐藏**，用于检查窗口是否处于隐藏状态。
    WCA_ACCENT_POLICY                 = 19, // - 设置窗口的强调色策略，最常用的枚举项，用于控制窗口的背景模糊、透明等效果（如 Windows 10/11 的亚克力效果）。
    WCA_FREEZE_REPRESENTATION         = 20, // - 冻结窗口的表示，用于在某些场景下冻结窗口的视觉表示（如截图时）。
    WCA_EVER_UNCLOAKED                = 21, // - 窗口是否从未被隐藏过，用于检查窗口是否一直处于可见状态。
    WCA_VISUAL_OWNER                  = 22, // - 设置窗口的视觉所有者**，用于指定窗口的视觉所有者（如子窗口的父窗口）。
    WCA_HOLOGRAPHIC                   = 23,
    WCA_EXCLUDED_FROM_DDA             = 24,
    WCA_PASSIVEUPDATEMODE             = 25,
    WCA_USEDARKMODECOLORS             = 26,
    WCA_CORNER_STYLE                  = 27,
    WCA_PART_COLOR                    = 28,
    WCA_DISABLE_MOVESIZE_FEEDBACK     = 29,
    WCA_LAST                          = 30,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttributeData
{
    public WindowCompositionAttrib Attribute;
    public IntPtr                  Data;
    public int                     SizeOfData;
}

internal enum AccentState
{
    // 启用透明后，上下边框会变成一条细线

    ACCENT_DISABLED                   = 0, // 实测，会停止绘制背景色，但边框仍然会绘制
    ACCENT_ENABLE_GRADIENT            = 1, // 实心边框和背景（可能忽略了Alpha通道）
    ACCENT_ENABLE_TRANSPARENTGRADIENT = 2, // 半透明实心边框和背景
    ACCENT_ENABLE_BLURBEHIND          = 3, // 半透明模糊背景色，搭配 AccentFlags 可实现玻璃效果
    ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4, // 半透明模糊背景色，搭配 AccentFlags 可实现亚克力效果（比玻璃更模糊），拖动很缓慢，原因待调查
    ACCENT_INVALID_STATE              = 5,
}

[Flags]
internal enum AccentFlags : uint
{
    None = 0x0, // 无特殊标志

    // EnableGradient    = 0x1,   // 启用渐变，已弃用，没有效果
    EnableAcrylicBlur = 0x2, // 模糊背景时，启用亚克力效果

    // 边框，在 ACCENT_ENABLE_ACRYLICBLURBEHIND 状态会出现双边框
    DrawLeftBorder   = 0x20,  // 绘制左侧边框
    DrawTopBorder    = 0x40,  // 绘制顶部边框
    DrawRightBorder  = 0x80,  // 绘制右侧边框
    DrawBottomBorder = 0x100, // 绘制底部边框
    DrawAllBorders   = DrawLeftBorder | DrawTopBorder | DrawRightBorder | DrawBottomBorder
}

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy
{
    public AccentState AccentState;
    public AccentFlags AccentFlags;
    public uint        GradientColor;
    public int         AnimationId;
}

//////////////////////////////////

// 主题属性标志
[Flags]
internal enum ThemeAttributeFlags : uint
{
    None                 = 0x0,
    UseImmersiveDarkMode = 0x1,
    UseAccentColor       = 0x2,
    EnableAcrylic        = 0x4,
    EnableMica           = 0x8,
    EnableTabbed         = 0x10
}

// 主题属性结构
[StructLayout(LayoutKind.Sequential)]
internal struct ThemeAttributeData
{
    public ThemeAttributeFlags Flags;
    public uint                ColorAttribute; // ARGB 格式的颜色
    public uint                AnimationId;
    public uint                Reserved; // 必须为 0
}

#pragma warning restore SA1649 // File name should match first type name