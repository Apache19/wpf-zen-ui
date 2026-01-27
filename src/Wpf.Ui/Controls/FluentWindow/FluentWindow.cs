// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A custom WinUI Window with more convenience methods.
/// </summary>
public class FluentWindow : System.Windows.Window
{
    private static readonly object _systemMetricCacheLock = new();
    private static volatile bool _systemMetricCacheInvalidated = true;
    private static int _cachedSmCxSizeFrame;
    private static int _cachedSmCySizeFrame;
    private static int _cachedSmCxPaddedBorder;

    private WindowInteropHelper? _interopHelper = null;

    /// <summary>
    /// Gets contains helper for accessing this window handle.
    /// </summary>
    protected WindowInteropHelper InteropHelper
    {
        get => _interopHelper ??= new WindowInteropHelper(this);
    }

    /// <summary>Identifies the <see cref="WindowCornerPreference"/> dependency property.</summary>
    public static readonly DependencyProperty WindowCornerPreferenceProperty = DependencyProperty.Register(
        nameof(WindowCornerPreference),
        typeof(WindowCornerPreference),
        typeof(FluentWindow),
        new PropertyMetadata(WindowCornerPreference.Round, OnWindowCornerPreferenceChanged)
    );

    /// <summary>Identifies the <see cref="WindowBackdropType"/> dependency property.</summary>
    public static readonly DependencyProperty WindowBackdropTypeProperty = DependencyProperty.Register(
        nameof(WindowBackdropType),
        typeof(WindowBackdropType),
        typeof(FluentWindow),
        new PropertyMetadata(WindowBackdropType.None, OnWindowBackdropTypeChanged)
    );

    /// <summary>Identifies the <see cref="ExtendsContentIntoTitleBar"/> dependency property.</summary>
    public static readonly DependencyProperty ExtendsContentIntoTitleBarProperty =
        DependencyProperty.Register(
            nameof(ExtendsContentIntoTitleBar),
            typeof(bool),
            typeof(FluentWindow),
            new PropertyMetadata(false, OnExtendsContentIntoTitleBarChanged)
        );

    /// <summary>
    /// Gets or sets a value determining corner preference for current <see cref="Window"/>.
    /// </summary>
    public WindowCornerPreference WindowCornerPreference
    {
        get => (WindowCornerPreference)GetValue(WindowCornerPreferenceProperty);
        set => SetValue(WindowCornerPreferenceProperty, value);
    }

    /// <summary>
    /// Gets or sets a value determining preferred backdrop type for current <see cref="Window"/>.
    /// </summary>
    public WindowBackdropType WindowBackdropType
    {
        get => (WindowBackdropType)GetValue(WindowBackdropTypeProperty);
        set => SetValue(WindowBackdropTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the default title bar of the window should be hidden to create space for app content.
    /// </summary>
    public bool ExtendsContentIntoTitleBar
    {
        get => (bool)GetValue(ExtendsContentIntoTitleBarProperty);
        set => SetValue(ExtendsContentIntoTitleBarProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentWindow"/> class.
    /// </summary>
    public FluentWindow()
    {
        SetResourceReference(StyleProperty, typeof(FluentWindow));
    }

    /// <summary>
    /// Initializes static members of the <see cref="FluentWindow"/> class.
    /// Overrides default properties.
    /// </summary>
    /// <remarks>
    /// Overrides default properties.
    /// </remarks>
    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        OnCornerPreferenceChanged(default, WindowCornerPreference);
        OnExtendsContentIntoTitleBarChanged(false, ExtendsContentIntoTitleBar);
        OnBackdropTypeChanged(default, WindowBackdropType);

        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome is not null)
            {
                chrome.ResizeBorderThickness = GetWindowBorderThickness(hwndSource.Handle);
            }
        }
    }

    // 包含 SM_CXPADDINGBORDER + SYSTEM_METRICS_INDEX.SM_CXSIZEFRAME/SYSTEM_METRICS_INDEX.SM_CYSIZEFRAME 的边框厚度，
    // 其中是否包含 SM_CXPADDINGBORDER 根据窗口是否有 WINDOW_STYLE.WS_CAPTION 风格决定
    private Thickness GetWindowBorderThickness(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return default;
        }

        var    dpi    = DpiHelper.GetSystemDpi();
        double scaleX = dpi.DpiX / (double)DpiHelper.DefaultDpi;
        double scaleY = dpi.DpiY / (double)DpiHelper.DefaultDpi;

        var paddingBorder = 0;

        WINDOW_STYLE style = 0;

        try
        {
            style = (WINDOW_STYLE)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        }
        catch
        {
            style = 0;
        }

        EnsureSystemMetricCache();

        if ((style & WINDOW_STYLE.WS_CAPTION) != 0)
        {
            paddingBorder = _cachedSmCxPaddedBorder;
        }

        var max = (style & WINDOW_STYLE.WS_MAXIMIZE) == WINDOW_STYLE.WS_MAXIMIZE;

        var frameX = _cachedSmCxSizeFrame;
        var frameY = _cachedSmCySizeFrame;

        return new Thickness(
            (paddingBorder + frameX) / scaleX,
            max ? (paddingBorder + frameY) / scaleY : 1, // 给顶部留1像素边框,最大化时则按全量
            (paddingBorder + frameX) / scaleX,
            (paddingBorder + frameY) / scaleY
        );
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        switch (uMsg)
        {
            case PInvoke.WM_WININICHANGE:
            {
                InvalidateSystemMetricCache();
                break;
            }

            // 纠正 WindowChrome 无脑将客户区内容放进所有非客户区的行为，我们只需要把客户区内容放进标题栏的功能。
            // 此外，WindowChrome 的非客户区计算方法已过时，它没有考虑 SM_CXPADDINGBORDER（带有标题的窗口有这个填充边框）。
            case PInvoke.WM_NCCALCSIZE:
            {
                // todo 当wParam==true时，窗口位置、大小可能发生了变化，此时应该读取窗口样式，根据最大化、最小化等状态重新计算客户区尺寸。
                // todo 最大化时系统会按照全量窗口边框将其放置在工作区外，所以此时计算客户区大小应该谨慎。
                // todo 关于在Win7系统里使用WindowChrome会导致边框黑屏，我怀疑就是因为WindowChrome强制将边框设为0，
                //      而系统需要绘制边框，但此时计算出的客户区与系统认为的窗口非客户区（边框）重叠而导致的。
                // todo 最终解决方案应该是不管什么系统，都应按照系统认为的窗口边框宽度来调整客户区大小，这样有2个好处，一是窗口可以像正常窗口一样
                //      调整大小，而不是在窗口内部，二是解决了Win7系统边框黑屏的问题
                // todo 实际上WindowChrome无脑让客户区内容放进“所有非客户区”的做法是错误的。真正应该实现的是只让客户区内容可以放进“标题栏”（不应干涉窗口边框）。
                var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

                var dpi    = DpiHelper.GetWindowDpi(hwnd);
                var border = DpiHelper.LogicalThicknessToDevice(GetWindowBorderThickness(hwnd), dpi.DpiScaleX, dpi.DpiScaleY) ;

                // Debug border
                Debug.WriteLine($"Border Thickness: Left={border.Left}, Top={border.Top}, Right={border.Right}, Bottom={border.Bottom}");

                rcClientArea.left   += (int)border.Left;
                rcClientArea.right  -= (int)border.Right;
                rcClientArea.top    += (int)border.Top;
                rcClientArea.bottom -= (int)border.Bottom;

                Marshal.StructureToPtr(rcClientArea, lParam, false);

                handled = true;

                IntPtr retVal = IntPtr.Zero;
                if (wParam.ToInt32() != 0) // wParam == TRUE
                {
                    retVal = (IntPtr)PInvoke.WVR_REDRAW;
                }

                return retVal;
            }
        }

        return IntPtr.Zero;
    }

    private static void InvalidateSystemMetricCache() => _systemMetricCacheInvalidated = true;

    private static void EnsureSystemMetricCache()
    {
        if (!_systemMetricCacheInvalidated)
        {
            return;
        }

        lock (_systemMetricCacheLock)
        {
            if (!_systemMetricCacheInvalidated)
            {
                return;
            }

            _cachedSmCxSizeFrame = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSIZEFRAME);
            _cachedSmCySizeFrame = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSIZEFRAME);
            _cachedSmCxPaddedBorder = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXPADDEDBORDER);

            _systemMetricCacheInvalidated = false;
        }
    }

    /// <summary>
    /// Private <see cref="WindowCornerPreference"/> property callback.
    /// </summary>
    private static void OnWindowCornerPreferenceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not FluentWindow window)
        {
            return;
        }

        if (e.OldValue == e.NewValue)
        {
            return;
        }

        window.OnCornerPreferenceChanged(
            (WindowCornerPreference)e.OldValue,
            (WindowCornerPreference)e.NewValue
        );
    }

    /// <summary>
    /// This virtual method is called when <see cref="WindowCornerPreference"/> is changed.
    /// </summary>
    protected virtual void OnCornerPreferenceChanged(
        WindowCornerPreference oldValue,
        WindowCornerPreference newValue
    )
    {
        if (InteropHelper.Handle == IntPtr.Zero)
        {
            return;
        }

        _ = UnsafeNativeMethods.ApplyWindowCornerPreference(InteropHelper.Handle, newValue);
    }

    /// <summary>
    /// Private <see cref="WindowBackdropType"/> property callback.
    /// </summary>
    private static void OnWindowBackdropTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FluentWindow window)
        {
            return;
        }

        if (e.OldValue == e.NewValue)
        {
            return;
        }

        window.OnBackdropTypeChanged((WindowBackdropType)e.OldValue, (WindowBackdropType)e.NewValue);
    }

    /// <summary>
    /// This virtual method is called when <see cref="WindowBackdropType"/> is changed.
    /// </summary>
    protected virtual void OnBackdropTypeChanged(WindowBackdropType oldValue, WindowBackdropType newValue)
    {
        if (Appearance.ApplicationThemeManager.GetAppTheme() == Appearance.ApplicationTheme.HighContrast)
        {
            newValue = WindowBackdropType.None;
        }

        if (InteropHelper.Handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowChrome();

        if (newValue == WindowBackdropType.None)
        {
            _ = WindowBackdrop.RemoveBackdrop(this);
            return;
        }

        if (!ExtendsContentIntoTitleBar)
        {
            throw new InvalidOperationException(
                $"Cannot apply backdrop effect if {nameof(ExtendsContentIntoTitleBar)} is false."
            );
        }

        if (WindowBackdrop.IsSupported(newValue) && WindowBackdrop.RemoveBackground(this))
        {
            _ = WindowBackdrop.ApplyBackdrop(this, newValue);

            _ = WindowBackdrop.RemoveTitlebarBackground(this);
        }
    }

    /// <summary>
    /// Private <see cref="ExtendsContentIntoTitleBar"/> property callback.
    /// </summary>
    private static void OnExtendsContentIntoTitleBarChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not FluentWindow window)
        {
            return;
        }

        if (e.OldValue == e.NewValue)
        {
            return;
        }

        window.OnExtendsContentIntoTitleBarChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    /// <summary>
    /// This virtual method is called when <see cref="ExtendsContentIntoTitleBar"/> is changed.
    /// </summary>
    protected virtual void OnExtendsContentIntoTitleBarChanged(bool oldValue, bool newValue)
    {
        // AllowsTransparency = true;
        SetCurrentValue(WindowStyleProperty, WindowStyle.SingleBorderWindow);

        // WindowStyleProperty.OverrideMetadata(typeof(FluentWindow), new FrameworkPropertyMetadata(WindowStyle.SingleBorderWindow));
        // AllowsTransparencyProperty.OverrideMetadata(typeof(FluentWindow), new FrameworkPropertyMetadata(false));
        _ = UnsafeNativeMethods.RemoveWindowTitlebarContents(this);
    }

    /// <summary>
    /// This virtual method is called when <see cref="WindowBackdropType"/> is changed.
    /// </summary>
    protected virtual void SetWindowChrome()
    {
        try
        {
            if (Utilities.IsCompositionEnabled)
            {
                WindowChrome.SetWindowChrome(
                    this,
                    new WindowChrome
                    {
                        CaptionHeight = 0,
                        CornerRadius  = default,
                        GlassFrameThickness =
                            WindowBackdropType == WindowBackdropType.None
                                ? new Thickness(0.00001)
                                : new Thickness(-1), // 0.00001 so there's no glass frame drawn around the window, but the border is still drawn.
                        ResizeBorderThickness =
                            ResizeMode == ResizeMode.NoResize ? default : new Thickness(8, 4, 8, 8),
                        UseAeroCaptionButtons = false,
                    }
                );
            }
        }
        catch (COMException)
        {
            // Ignored.
        }
    }
}