// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

extern alias UiNative;

using RECT = UiNative::Windows.Win32.Foundation.RECT;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.FluentWindow;

public class FluentWindowNonClientAreaTests
{
    [Fact]
    public void WindowFrameInsets_ClampEveryNegativeMeasurement()
    {
        var frame = new WindowFrameInsets(-1, -2, -3, -4);

        Assert.Equal(0, frame.Left);
        Assert.Equal(0, frame.Top);
        Assert.Equal(0, frame.Right);
        Assert.Equal(0, frame.Bottom);
    }

    [Fact]
    public void MeasureFrame_UsesPhysicalSideAndBottomOffsets()
    {
        var window = Rect(-1600, -900, -600, -200);
        var nativeClient = Rect(-1592, -861, -611, -209);

        var frame = FluentWindowNonClientArea.MeasureFrame(window, nativeClient);

        Assert.Equal(8, frame.Left);
        Assert.Equal(9, frame.Top);
        Assert.Equal(11, frame.Right);
        Assert.Equal(9, frame.Bottom);
    }

    [Theory]
    [InlineData(true, true, true, 0)]
    [InlineData(true, true, false, 1)]
    [InlineData(false, true, true, 2)]
    [InlineData(false, true, false, 3)]
    [InlineData(false, false, true, 3)]
    [InlineData(false, false, false, 3)]
    public void SelectFrameStrategy_AppliesNativeSwitchOnWindows10AndWindows11(
        bool isWindows11OrGreater,
        bool isWindows10OrGreater,
        bool useNativeWindowBorder,
        int expectedStrategyValue
    )
    {
        var strategy = FluentWindowNonClientArea.SelectFrameStrategy(
            isWindows11OrGreater,
            isWindows10OrGreater,
            useNativeWindowBorder
        );

        Assert.Equal((FluentWindowFrameStrategy)expectedStrategyValue, strategy);
    }

    [Theory]
    [InlineData(0, 108, 200, 889, 691)]
    [InlineData(1, 100, 200, 900, 700)]
    [InlineData(2, 108, 200, 889, 691)]
    [InlineData(3, 100, 200, 900, 700)]
    public void CalculateClientRect_NormalState_AppliesSelectedStrategy(
        int strategyValue,
        int expectedLeft,
        int expectedTop,
        int expectedRight,
        int expectedBottom
    )
    {
        var window = Rect(100, 200, 900, 700);
        var frame = new WindowFrameInsets(8, 9, 11, 9);

        var strategy = (FluentWindowFrameStrategy)strategyValue;
        var client = FluentWindowNonClientArea.CalculateClientRect(window, frame, strategy, false);

        AssertRect(client, expectedLeft, expectedTop, expectedRight, expectedBottom);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CalculateClientRect_MaximizedState_AlwaysPreservesMeasuredFrame(int strategyValue)
    {
        var window = Rect(-8, -9, 1928, 1089);
        var frame = new WindowFrameInsets(8, 9, 8, 9);
        var strategy = (FluentWindowFrameStrategy)strategyValue;

        var client = FluentWindowNonClientArea.CalculateClientRect(window, frame, strategy, true);

        AssertRect(client, 0, 0, 1920, 1080);
    }

    [Fact]
    public void MeasureFrame_UsesEveryCurrentCalculationWithoutCachingPreviousDpi()
    {
        var window = Rect(0, 0, 1000, 800);
        var first = FluentWindowNonClientArea.MeasureFrame(window, Rect(8, 31, 992, 792));
        var second = FluentWindowNonClientArea.MeasureFrame(window, Rect(12, 46, 988, 788));

        Assert.Equal(8, first.Left);
        Assert.Equal(8, first.Top);
        Assert.Equal(12, second.Left);
        Assert.Equal(12, second.Top);
    }

    [Fact]
    public void MeasureFrame_ClampsClientRectThatExtendsOutsideWindow()
    {
        var frame = FluentWindowNonClientArea.MeasureFrame(
            Rect(100, 100, 500, 400),
            Rect(90, 80, 520, 430)
        );

        Assert.Equal(0, frame.Left);
        Assert.Equal(0, frame.Top);
        Assert.Equal(0, frame.Right);
        Assert.Equal(0, frame.Bottom);
    }

    [Theory]
    [InlineData(0, WindowState.Normal, 0, 0, 0, 0)]
    [InlineData(1, WindowState.Normal, 0, 0, 0, 0)]
    [InlineData(2, WindowState.Normal, 0, 1, 0, 0)]
    [InlineData(3, WindowState.Normal, 1, 1, 1, 1)]
    [InlineData(0, WindowState.Maximized, 0, 0, 0, 0)]
    [InlineData(1, WindowState.Maximized, 0, 0, 0, 0)]
    [InlineData(2, WindowState.Maximized, 0, 0, 0, 0)]
    [InlineData(3, WindowState.Maximized, 0, 0, 0, 0)]
    public void CalculateVisualBorderThickness_TracksStrategyAndWindowState(
        int strategyValue,
        WindowState windowState,
        double left,
        double top,
        double right,
        double bottom
    )
    {
        var thickness = FluentWindowNonClientArea.CalculateVisualBorderThickness(
            (FluentWindowFrameStrategy)strategyValue,
            windowState
        );

        Assert.Equal(new Thickness(left, top, right, bottom), thickness);
    }

    [Theory]
    [InlineData(0, false, 0, 0, 0, 0)]
    [InlineData(1, false, 1, 1, 1, 1)]
    [InlineData(2, false, 0, 0, 0, 0)]
    [InlineData(3, false, 0, 0, 0, 0)]
    [InlineData(0, true, -1, -1, -1, -1)]
    [InlineData(1, true, -1, -1, -1, -1)]
    public void CalculateDwmFrameMargins_PreservesWin11ClientFrameWithoutBackdrop(
        int strategyValue,
        bool isBackdropApplied,
        int left,
        int top,
        int right,
        int bottom
    )
    {
        var margins = FluentWindowNonClientArea.CalculateDwmFrameMargins(
            (FluentWindowFrameStrategy)strategyValue,
            isBackdropApplied
        );

        Assert.Equal(left, margins.cxLeftWidth);
        Assert.Equal(top, margins.cyTopHeight);
        Assert.Equal(right, margins.cxRightWidth);
        Assert.Equal(bottom, margins.cyBottomHeight);
    }

    [Theory]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, true, true, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, true, true, true, false)]
    public void ShouldApplyDwmAccentBorder_RequiresActiveClientFrameAndSystemSetting(
        bool useNativeWindowBorder,
        bool isNcActive,
        bool isAccentOnTitleBarAndBorder,
        bool isHighContrast,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            FluentWindowNonClientArea.ShouldApplyDwmAccentBorder(
                useNativeWindowBorder,
                isNcActive,
                isAccentOnTitleBarAndBorder,
                isHighContrast
            )
        );
    }

    [Theory]
    [InlineData(0xFF112233u, 0x00332211u)]
    [InlineData(0x000078D4u, 0x00D47800u)]
    [InlineData(0xFFFFFFFFu, 0x00FFFFFFu)]
    public void ConvertDwmColorizationColorToColorRef_SwapsRedAndBlue(
        uint colorizationColor,
        uint expectedColorRef
    )
    {
        Assert.Equal(
            expectedColorRef,
            FluentWindowNonClientArea.ConvertDwmColorizationColorToColorRef(colorizationColor)
        );
    }

    [Fact]
    public void CalculateClientRect_ZeroFrame_RemainsStable()
    {
        var window = Rect(-400, -300, 400, 300);
        var client = FluentWindowNonClientArea.CalculateClientRect(
            window,
            new WindowFrameInsets(0, 0, 0, 0),
            FluentWindowFrameStrategy.Windows11Native,
            false
        );

        AssertRect(client, -400, -300, 400, 300);
    }

    [Theory]
    [InlineData(-500, -300, 13)]
    [InlineData(-300, -300, 12)]
    [InlineData(-101, -300, 14)]
    [InlineData(-500, -150, 10)]
    [InlineData(-101, -150, 11)]
    [InlineData(-500, -1, 16)]
    [InlineData(-300, -1, 15)]
    [InlineData(-101, -1, 17)]
    [InlineData(-300, -150, 0)]
    public void HitTestResizeBorder_MapsAllEdgesCornersAndClient(double x, double y, int expectedHitTest)
    {
        var result = FluentWindowNonClientArea.HitTestResizeBorder(
            Rect(-500, -300, -100, 0),
            new Point(x, y),
            new WindowFrameInsets(8, 9, 11, 9)
        );

        Assert.Equal((IntPtr)expectedHitTest, result);
    }

    [Theory]
    [InlineData(7, 8, 13)]
    [InlineData(8, 8, 12)]
    [InlineData(388, 8, 12)]
    [InlineData(389, 8, 14)]
    [InlineData(8, 9, 0)]
    [InlineData(389, 290, 17)]
    [InlineData(388, 290, 15)]
    public void HitTestResizeBorder_UsesHalfOpenAsymmetricBoundaries(
        double x,
        double y,
        int expectedHitTest
    )
    {
        var result = FluentWindowNonClientArea.HitTestResizeBorder(
            Rect(0, 0, 400, 300),
            new Point(x, y),
            new WindowFrameInsets(8, 9, 11, 10)
        );

        Assert.Equal((IntPtr)expectedHitTest, result);
    }

    [Theory]
    [InlineData(TitleBarHitTestRole.None, false, 0)]
    [InlineData(TitleBarHitTestRole.Client, false, 1)]
    [InlineData(TitleBarHitTestRole.Caption, false, 2)]
    [InlineData(TitleBarHitTestRole.Icon, false, 2)]
    [InlineData(TitleBarHitTestRole.Icon, true, 3)]
    [InlineData(TitleBarHitTestRole.MinimizeButton, false, 8)]
    [InlineData(TitleBarHitTestRole.MaximizeButton, false, 9)]
    [InlineData(TitleBarHitTestRole.CloseButton, false, 20)]
    [InlineData(TitleBarHitTestRole.HelpButton, false, 21)]
    public void MapTitleBarRole_MapsEverySemanticRole(
        TitleBarHitTestRole role,
        bool isIconInteractionEnabled,
        int expectedHitTest
    )
    {
        var result = FluentWindowNonClientArea.MapTitleBarRole(role, isIconInteractionEnabled);

        Assert.Equal((IntPtr)expectedHitTest, result);
    }

    private static RECT Rect(int left, int top, int right, int bottom)
    {
        return new RECT
        {
            left = left,
            top = top,
            right = right,
            bottom = bottom,
        };
    }

    private static void AssertRect(RECT actual, int left, int top, int right, int bottom)
    {
        Assert.Equal(left, actual.left);
        Assert.Equal(top, actual.top);
        Assert.Equal(right, actual.right);
        Assert.Equal(bottom, actual.bottom);
    }
}
