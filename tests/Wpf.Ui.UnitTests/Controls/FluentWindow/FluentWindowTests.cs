// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Win32;

namespace Wpf.Ui.UnitTests.Controls.FluentWindow;

[Collection(nameof(ThemeManagerTestCollection))]
public class FluentWindowTests
{
    [Fact]
    public void DependencyProperties_ExposeDocumentedDefaultsAndReadOnlyState()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow();

            Assert.Equal(WindowState.Normal, window.ActualWindowState);
            Assert.False(window.IsNcActive);
            Assert.True(Wpf.Ui.Controls.FluentWindow.ActualWindowStateProperty.ReadOnly);
            Assert.True(Wpf.Ui.Controls.FluentWindow.IsNcActiveProperty.ReadOnly);
            Assert.True(Wpf.Ui.Controls.FluentWindow.IsAccentOnTitleBarAndBorderProperty.ReadOnly);
            Assert.True(window.UseNativeWindowBorder);
            Assert.True(window.IsTitleBarHitTestVisibleWhenCovered);
            Assert.Equal(WindowBackdropType.None, window.WindowBackdropType);
            Assert.Equal(WindowCornerPreference.Default, window.WindowCornerPreference);
            Assert.Throws<InvalidOperationException>(() =>
                window.SetValue(Wpf.Ui.Controls.FluentWindow.ActualWindowStateProperty, WindowState.Maximized)
            );
            Assert.Throws<InvalidOperationException>(() =>
                window.SetValue(Wpf.Ui.Controls.FluentWindow.IsNcActiveProperty, true)
            );
            Assert.Throws<InvalidOperationException>(() =>
                window.SetValue(
                    Wpf.Ui.Controls.FluentWindow.IsAccentOnTitleBarAndBorderProperty,
                    true
                )
            );
        });
    }

    [Fact]
    public void ConfigurableDependencyProperties_RoundTripValuesWithoutCreatingAnHwnd()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow
            {
                UseNativeWindowBorder = false,
                IsTitleBarHitTestVisibleWhenCovered = false,
                WindowBackdropType = WindowBackdropType.Mica_Alt,
                WindowCornerPreference = WindowCornerPreference.RoundSmall,
            };

            Assert.False(window.UseNativeWindowBorder);
            Assert.False(window.IsTitleBarHitTestVisibleWhenCovered);
            Assert.Equal(WindowBackdropType.Mica_Alt, window.WindowBackdropType);
            Assert.Equal(WindowCornerPreference.RoundSmall, window.WindowCornerPreference);
        });
    }

    [Fact]
    public void BorderThickness_IsAComputedFrameOutputAndIgnoresLocalDecorationValues()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow
            {
                BorderThickness = new Thickness(20, 30, 40, 50),
            };

            var expected = FluentWindowNonClientArea.CalculateVisualBorderThickness(
                FluentWindowNonClientArea.SelectFrameStrategy(
                    Utilities.IsWin_11_Or_Greater,
                    Utilities.IsWin_10_Or_Greater,
                    true
                ),
                WindowState.Normal
            );
            Assert.Equal(expected, window.BorderThickness);

            window.UseNativeWindowBorder = false;
            expected = FluentWindowNonClientArea.CalculateVisualBorderThickness(
                FluentWindowNonClientArea.SelectFrameStrategy(
                    Utilities.IsWin_11_Or_Greater,
                    Utilities.IsWin_10_Or_Greater,
                    false
                ),
                WindowState.Normal
            );
            Assert.Equal(expected, window.BorderThickness);
        });
    }

    [Fact]
    public void NativeSizeState_UpdatesReadOnlyStateAndRecoercesBorder()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow { UseNativeWindowBorder = false };

            InvokePrivate(window, "UpdateActualWindowState", 2);
            Assert.Equal(WindowState.Maximized, window.ActualWindowState);
            Assert.Equal(new Thickness(0), window.BorderThickness);

            InvokePrivate(window, "UpdateActualWindowState", 1);
            Assert.Equal(WindowState.Minimized, window.ActualWindowState);

            InvokePrivate(window, "UpdateActualWindowState", 0);
            Assert.Equal(WindowState.Normal, window.ActualWindowState);
            Assert.Equal(
                FluentWindowNonClientArea.CalculateVisualBorderThickness(
                    FluentWindowNonClientArea.SelectFrameStrategy(
                        Utilities.IsWin_11_Or_Greater,
                        Utilities.IsWin_10_Or_Greater,
                        false
                    ),
                    WindowState.Normal
                ),
                window.BorderThickness
            );

            InvokePrivate(window, "UpdateActualWindowState", 99);
            Assert.Equal(WindowState.Normal, window.ActualWindowState);
        });
    }

    [Fact]
    public void DefaultTemplate_ContainsFrameAndResizeGripWithStateDrivenVisibility()
    {
        RunOnApplicationThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateGenericResourceUri() };
            var window = new Wpf.Ui.Controls.FluentWindow
            {
                ResizeMode = ResizeMode.CanResizeWithGrip,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.FluentWindow)]),
            };

            Assert.True(window.ApplyTemplate());

            var border = Assert.IsType<Border>(window.Template.FindName("WindowBorder", window));
            var resizeGrip = Assert.IsType<ResizeGrip>(
                window.Template.FindName("WindowResizeGrip", window)
            );
            Assert.Equal(window.BorderThickness, border.BorderThickness);
            Assert.Equal(Visibility.Visible, resizeGrip.Visibility);

            InvokePrivate(window, "UpdateActualWindowState", 2);
            Assert.Equal(Visibility.Collapsed, resizeGrip.Visibility);

            InvokePrivate(window, "UpdateActualWindowState", 0);
            Assert.Equal(Visibility.Visible, resizeGrip.Visibility);

            window.ResizeMode = ResizeMode.CanResize;
            Assert.Equal(Visibility.Collapsed, resizeGrip.Visibility);
        });
    }

    [Fact]
    public void BackgroundCoercion_IsTransparentOnlyWhileWin11BackdropIsApplied()
    {
        RunOnApplicationThread(() =>
        {
            var requestedBackground = new SolidColorBrush(Color.FromRgb(0x21, 0x43, 0x65));
            var window = new Wpf.Ui.Controls.FluentWindow { Background = requestedBackground };

            SetPrivateField(window, "_isBackdropApplied", true);
            window.CoerceValue(Control.BackgroundProperty);

            if (Utilities.IsWin_11_Or_Greater)
            {
                var coerced = Assert.IsType<SolidColorBrush>(window.Background);
                Assert.Equal(Color.FromArgb(0, 0, 0, 0), coerced.Color);
                Assert.True(coerced.IsFrozen);
            }
            else
            {
                Assert.Same(requestedBackground, window.Background);
            }

            SetPrivateField(window, "_isBackdropApplied", false);
            window.CoerceValue(Control.BackgroundProperty);
            Assert.Same(requestedBackground, window.Background);
        });
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void CaptionButtonClassification_RecognizesOnlyCaptionButtons(int hitTest, bool expected)
    {
        RunOnApplicationThread(() =>
        {
            Assert.Equal(expected, InvokePrivateStatic<bool>("IsCaptionButtonHit", hitTest));
        });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-32768, 32767)]
    [InlineData(32767, -32768)]
    [InlineData(-123, -456)]
    public void PackedScreenPoint_SignExtendsBothPhysicalCoordinates(int x, int y)
    {
        RunOnApplicationThread(() =>
        {
            var packed = (long)(ushort)x | ((long)(ushort)y << 16);
            var point = InvokePrivateStatic<Point>("LParamToScreenPoint", new IntPtr(packed));

            Assert.Equal(x, point.X);
            Assert.Equal(y, point.Y);
        });
    }

    [Fact]
    public void SystemMenuStyleGuard_RemovesOnlySystemMenuBitFromStyleChanges()
    {
        RunOnApplicationThread(() =>
        {
            var memory = Marshal.AllocHGlobal(sizeof(uint) * 2);

            try
            {
                const uint oldStyle = 0x00CF0000;
                const uint requestedStyle = 0x10CF0000;
                const uint systemMenu = 0x00080000;
                Marshal.WriteInt32(memory, 0, unchecked((int)oldStyle));
                Marshal.WriteInt32(memory, sizeof(uint), unchecked((int)requestedStyle));

                InvokePrivateStatic<object?>(
                    "PreventSystemMenuStyle",
                    memory,
                    new IntPtr(-16)
                );

                Assert.Equal(oldStyle, unchecked((uint)Marshal.ReadInt32(memory, 0)));
                Assert.Equal(
                    requestedStyle & ~systemMenu,
                    unchecked((uint)Marshal.ReadInt32(memory, sizeof(uint)))
                );

                Marshal.WriteInt32(memory, sizeof(uint), unchecked((int)requestedStyle));
                InvokePrivateStatic<object?>("PreventSystemMenuStyle", memory, new IntPtr(-20));
                Assert.Equal(requestedStyle, unchecked((uint)Marshal.ReadInt32(memory, sizeof(uint))));
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        });
    }

    [Fact]
    public void BackdropSupport_UsesDocumentedOperatingSystemGates()
    {
        RunOnApplicationThread(() =>
        {
            Assert.True(InvokePrivateStatic<bool>("IsSupportedBackdrop", WindowBackdropType.None));
            Assert.Equal(
                Utilities.IsWin_11_22H2_Or_Greater,
                InvokePrivateStatic<bool>("IsSupportedBackdrop", WindowBackdropType.Auto)
            );
            Assert.Equal(
                Utilities.IsWin_11_22H2_Or_Greater,
                InvokePrivateStatic<bool>("IsSupportedBackdrop", WindowBackdropType.Mica_Alt)
            );
            Assert.Equal(
                Utilities.IsWin_10_Or_Greater,
                InvokePrivateStatic<bool>("IsSupportedBackdrop", WindowBackdropType.Mica)
            );
            Assert.Equal(
                Utilities.IsWin_7_Or_Greater,
                InvokePrivateStatic<bool>("IsSupportedBackdrop", WindowBackdropType.Acrylic)
            );
            Assert.False(
                InvokePrivateStatic<bool>("IsSupportedBackdrop", (WindowBackdropType)int.MaxValue)
            );
        });
    }

    [Fact]
    public void CaptionButtonHotState_MovesBetweenButtonsAndClearsIdempotently()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow();
            var first = new TitleBarButton();
            var second = new TitleBarButton();

            InvokePrivate(window, "SetHotCaptionButton", first);
            Assert.True(first.IsPointerOver);
            Assert.False(second.IsPointerOver);

            InvokePrivate(window, "SetHotCaptionButton", second);
            Assert.False(first.IsPointerOver);
            Assert.True(second.IsPointerOver);

            InvokePrivate(window, "SetHotCaptionButton", second);
            Assert.True(second.IsPointerOver);

            InvokePrivate(window, "SetHotCaptionButton", new object?[] { null });
            Assert.False(first.IsPointerOver);
            Assert.False(second.IsPointerOver);
        });
    }

    [Fact]
    public void RestoreNonClientMouseTracking_ClearsPendingPressWithoutAnHwnd()
    {
        RunOnApplicationThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow();
            SetPrivateField(window, "_pressedCaptionButton", new TitleBarButton());

            window.RestoreNonClientMouseTracking();

            Assert.Null(GetPrivateField(window, "_pressedCaptionButton"));
            Assert.False(Assert.IsType<bool>(GetPrivateField(window, "_isTrackingNonClientMouse")));
        });
    }

    [Fact]
    public void ClosedBeforeSourceInitialization_IsSafeAndLeavesNativeStateCleared()
    {
        RunOnApplicationThread(() =>
        {
            var window = new TestFluentWindow();

            window.InvokeOnClosed();

            Assert.Null(GetPrivateField(window, "_hwndSource"));
            Assert.False(Assert.IsType<bool>(GetPrivateField(window, "_isThemeSubscribed")));
        });
    }

    private static Uri CreateGenericResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void RunOnApplicationThread(Action action)
    {
        WpfApplicationTestHost.Run(action);
    }

    private static void InvokePrivate(object target, string methodName, params object?[]? arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        ) ?? typeof(Wpf.Ui.Controls.FluentWindow).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.NotNull(method);
        method.Invoke(target, arguments);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[]? arguments)
    {
        var method = typeof(Wpf.Ui.Controls.FluentWindow).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic
        );

        Assert.NotNull(method);
        var result = method.Invoke(null, arguments);

        if (result is null)
        {
            return default!;
        }

        return Assert.IsType<T>(result);
    }

    private static object? GetPrivateField(object target, string fieldName)
    {
        var field = typeof(Wpf.Ui.Controls.FluentWindow).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.NotNull(field);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = typeof(Wpf.Ui.Controls.FluentWindow).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private sealed class TestFluentWindow : Wpf.Ui.Controls.FluentWindow
    {
        public void InvokeOnClosed()
        {
            OnClosed(EventArgs.Empty);
        }
    }
}