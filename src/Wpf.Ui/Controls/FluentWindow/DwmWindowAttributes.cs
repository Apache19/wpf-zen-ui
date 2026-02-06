// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal sealed class DwmWindowAttributes
{
    private readonly Queue<IDwmAttributeSetter> _queue = new();
    private          bool                       _isAttached;

    public DwmWindowAttributes NcRenderingPolicy(DWMNCRENDERINGPOLICY policy)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, policy);
        return this;
    }

    public DwmWindowAttributes TransitionsForceDisabled(bool disabled)
    {
        // DWM 用 BOOL，本质是 int
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, disabled ? 1 : 0);
        return this;
    }

    public DwmWindowAttributes AllowNcPaint(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_ALLOW_NCPAINT, enabled ? 1 : 0);
        return this;
    }

    public DwmWindowAttributes NonClientRtlLayout(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_NONCLIENT_RTL_LAYOUT, enabled ? 1 : 0);
        return this;
    }

    /// <summary>
    /// 让窗口任务栏略缩图只显示一个图标，而不是窗口内容预览
    /// </summary>
    public DwmWindowAttributes ForceIconicRepresentation(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_FORCE_ICONIC_REPRESENTATION, enabled ? 1 : 0);
        return this;
    }

    public DwmWindowAttributes Flip3DPolicy(DWMFLIP3DWINDOWPOLICY policy)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_FLIP3D_POLICY, (int)policy);
        return this;
    }

    public DwmWindowAttributes HasIconicBitmap(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_HAS_ICONIC_BITMAP, enabled ? 1 : 0);
        return this;
    }

    // Vista 及更早版本不支持
    public DwmWindowAttributes DisallowPeek(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_DISALLOW_PEEK, enabled ? 1 : 0);
        return this;
    }

    // Vista 及更早版本不支持
    public DwmWindowAttributes ExcludedFromPeek(bool excluded)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_EXCLUDED_FROM_PEEK, excluded ? 1 : 0);
        return this;
    }

    // 7 及更早版本不支持
    public DwmWindowAttributes Cloak(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_CLOAK, enabled ? 1 : 0);
        return this;
    }

    // DWMWA_FREEZE_REPRESENTATION 7 及更早版本： 不支持此值。
    public DwmWindowAttributes FreezeRepresentation(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_FREEZE_REPRESENTATION, enabled ? 1 : 0);
        return this;
    }

    // DWMWA_USE_HOSTBACKDROPBRUSH 此值从 Windows 11 Build 22000 开始受支持。
    // 此方法允许非 UWP 窗口使用宿主背景画笔。
    // 如果设置此标志，则调用Windows::UI::Composition API 的 Win32 应用可以使用宿主背景画笔构建透明效果
    // （请参阅Compositor.CreateHostBackdropBrush）。
    // pvAttribute参数指向一个BOOL类型的值。
    // 设置为TRUE表示启用窗口的宿主背景画笔，设置为FALSE表示禁用。
    public DwmWindowAttributes UseHostBackdropBrush(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_USE_HOSTBACKDROPBRUSH, enabled ? 1 : 0);
        return this;
    }

    // DWMWA_USE_IMMERSIVE_DARK_MODE 此值从 Windows 11 Build 22000 开始受支持。
    // 当系统启用深色模式时，允许此窗口的窗口边框以深色模式颜色绘制。
    // 出于兼容性考虑，所有窗口默认都使用浅色模式，无论系统设置如何。
    // pvAttribute参数指向一个BOOL类型的值。
    // TRUE表示窗口遵循深色模式，FALSE表示始终使用浅色模式。
    public DwmWindowAttributes UseImmersiveDarkMode(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, enabled ? 1 : 0);
        return this;
    }

    // DWMWA_WINDOW_CORNER_PREFERENCE 此值从 Windows 11 Build 22000 开始受支持。
    // 指定窗口的圆角首选项。
    // pvAttribute参数指向DWM_WINDOW_CORNER_PREFERENCE类型的值。
    public DwmWindowAttributes WindowCornerPreference(DWM_WINDOW_CORNER_PREFERENCE cornerPreference)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreference);
        return this;
    }

    // DWMWA_BORDER_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 指定窗口边框颜色。pvAttribute参数指向一个COLORREF类型的值。应用程序负责根据状态变化（例如窗口激活状态的改变）更改边框颜色。
    // 将颜色指定为DWMWA_COLOR_NONE（值为 0xFFFFFFFE）将禁止绘制窗口边框。这样就可以创建无边框的圆角窗口。
    // 将颜色指定为DWMWA_COLOR_DEFAULT（值为 0xFFFFFFFF）会将窗口重置为使用系统默认的边框颜色。
    public DwmWindowAttributes BorderColor(uint colorRef)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, colorRef);
        return this;
    }

    // DWMWA_CAPTION_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统标题颜色的默认行为。
    public DwmWindowAttributes CaptionColor(uint colorRef)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, colorRef);
        return this;
    }

    // DWMWA_TEXT_COLOR 此值从 Windows 11 Build 22000 开始受支持。
    // 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统对标题文本颜色的默认行为。
    public DwmWindowAttributes TextColor(uint colorRef)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_TEXT_COLOR, colorRef);
        return this;
    }

    // DWMWA_SYSTEMBACKDROP_TYPE DWM_SYSTEMBACKDROP_TYPE enum 此值从 Windows 11 内部版本 22621 开始受支持。
    // 检索或指定窗口的系统绘制背景材质，包括非客户区后面的背景。pvAttribute参数指向DWM_SYSTEMBACKDROP_TYPE类型的值。
    public DwmWindowAttributes SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE backdrop)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdrop);
        return this;
    }

    public void SetToWindow(Window window)
    {
        if (_isAttached)
        {
            return;
        }

        _isAttached = true;

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            ApplyToHwnd((HWND)source.Handle);
        }
        else
        {
            window.SourceInitialized += OnSourceInitialized;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= OnSourceInitialized;

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            ApplyToHwnd((HWND)source.Handle);
        }
    }

    private void ApplyToHwnd(HWND hwnd)
    {
        while (_queue.Count > 0)
        {
            _queue.Dequeue().Apply(hwnd);
        }
    }

    private void Enqueue<T>(DWMWINDOWATTRIBUTE attribute, T value)
        where T : unmanaged
    {
        _queue.Enqueue(new DwmAttributeSetter<T>(attribute, value));
    }
}