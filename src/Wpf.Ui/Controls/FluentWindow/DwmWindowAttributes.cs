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

    public DwmWindowAttributes SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE backdrop)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdrop);
        return this;
    }

    public DwmWindowAttributes UseImmersiveDarkMode(bool enabled)
    {
        Enqueue(DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, enabled ? 1 : 0);
        return this;
    }

    // 请增加 DWMWA_ALLOW_NCPAINT bool
    // DWMWA_NONCLIENT_RTL_LAYOUT bool
    // DWMWA_FORCE_ICONIC_REPRESENTATION bool
    // DWMWA_FLIP3D_POLICY DWMFLIP3DWINDOWPOLICY enum
    // DWMWA_HAS_ICONIC_BITMAP bool
    // DWMWA_DISALLOW_PEEK bool
    // DWMWA_EXCLUDED_FROM_PEEK bool
    // DWMWA_CLOAK bool
    // DWMWA_FREEZE_REPRESENTATION bool
    // DWMWA_USE_HOSTBACKDROPBRUSH bool 此值从 Windows 11 内部版本 22000 开始受支持。
    // DWMWA_USE_IMMERSIVE_DARK_MODE bool
    // DWMWA_WINDOW_CORNER_PREFERENCE DWM_WINDOW_CORNER_PREFERENCE enum
    // DWMWA_BORDER_COLOR  COLORREF 为颜色指定 DWMWA_COLOR_NONE（值0xFFFFFFFE）将抑制窗口边框的绘制。 这样一来，可以有一个没有边框的圆窗口。
    //    为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置回使用系统对边框颜色的默认行为。
    // DWMWA_CAPTION_COLOR 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统标题颜色的默认行为。
    // DWMWA_TEXT_COLOR 为颜色指定 DWMWA_COLOR_DEFAULT（值0xFFFFFFFF）会将窗口重置为使用系统对标题文本颜色的默认行为。
    // DWMWA_SYSTEMBACKDROP_TYPE DWM_SYSTEMBACKDROP_TYPE enum 此值从 Windows 11 内部版本 22621 开始受支持。

    public void SetToWindow(Window window)
    {
        // todo 调查哈希重复概率，考虑存hash还是实例，用于这些属性的getter
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