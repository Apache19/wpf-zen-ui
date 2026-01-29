// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;
using static Wpf.Ui.Appearance.UISettingsRCW;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A custom WinUI Window with more convenience methods.
/// </summary>
public class FluentWindow : Window
{
    private WindowInteropHelper? _interopHelper       = null;
    private DwmWindowAttributes  _dwmWindowAttributes = new DwmWindowAttributes();

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
        new PropertyMetadata(
            WindowCornerPreference.Round // ,
            // OnWindowCornerPreferenceChanged
        )
    );

    /// <summary>Identifies the <see cref="WindowBackdropType"/> dependency property.</summary>
    public static readonly DependencyProperty WindowBackdropTypeProperty = DependencyProperty.Register(
        nameof(WindowBackdropType),
        typeof(WindowBackdropType),
        typeof(FluentWindow),
        new PropertyMetadata(
            WindowBackdropType.None // ,
            // OnWindowBackdropTypeChanged
        )
    );

    /// <summary>Identifies the <see cref="ExtendsContentIntoTitleBar"/> dependency property.</summary>
    public static readonly DependencyProperty ExtendsContentIntoTitleBarProperty =
        DependencyProperty.Register(
            nameof(ExtendsContentIntoTitleBar),
            typeof(bool),
            typeof(FluentWindow),
            new PropertyMetadata(
                false // ,
                // OnExtendsContentIntoTitleBarChanged
            )
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

        if (Utilities.IsOSWindows11OrNewer)
        {
            ApplicationThemeManager.Changed += (_, _) =>
            {
                if (IsActive && ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled)
                {
                    UnsafeNativeMethods.ApplyBorderColor(this, ApplicationAccentColorManager.SystemAccent);
                }
            };
        }
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

    /// <inheritdoc />
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (Utilities.IsOSWindows11OrNewer && ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled)
        {
            UnsafeNativeMethods.ApplyBorderColor(this, ApplicationAccentColorManager.SystemAccent);
        }
    }

    /// <inheritdoc />
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        if (Utilities.IsOSWindows11OrNewer)
        {
            // DWMWA_COLOR_DEFAULT.
            UnsafeNativeMethods.ApplyBorderColor(this, unchecked((int)0xFFFFFFFF));
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
        if (ApplicationThemeManager.GetAppTheme() == ApplicationTheme.HighContrast)
        {
            newValue = WindowBackdropType.None;
        }

        if (InteropHelper.Handle == IntPtr.Zero)
        {
            return;
        }

        // Win10 不设置 WindowChrome 可以正常绘制边框
        // Win11 需要设置 WindowChrome 才能绘制背景效果
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

        // window.OnExtendsContentIntoTitleBarChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    /// <summary>
    /// This virtual method is called when <see cref="ExtendsContentIntoTitleBar"/> is changed.
    /// </summary>
    protected virtual void OnExtendsContentIntoTitleBarChanged(bool oldValue, bool newValue)
    {
        // AllowsTransparency = true;
        // SetCurrentValue(WindowStyleProperty, WindowStyle.SingleBorderWindow);

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