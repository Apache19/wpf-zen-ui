// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Automation;
using WindowVisualState = FlaUI.Core.Definitions.WindowVisualState;

namespace Wpf.Ui.Gallery.IntegrationTests;

public sealed class TitleBarTests : UiTest
{
    private const int GwlStyle = -16;
    private const int WsSysMenu = 0x00080000;

    [Fact]
    public async Task FluentWindow_ShouldNotExposeNativeCaptionButtons_InNormalOrMaximizedState()
    {
        var hwnd = new IntPtr(MainWindow!.Properties.NativeWindowHandle.Value);

        (NativeMethods.GetWindowLong(hwnd, GwlStyle) & WsSysMenu)
            .Should()
            .Be(0, "because FluentWindow hosts its own title-bar buttons and system-menu entry points");

        MainWindow.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
        await Wait(1, TestContext.Current.CancellationToken);

        (NativeMethods.GetWindowLong(hwnd, GwlStyle) & WsSysMenu)
            .Should()
            .Be(0, "because maximizing must not allow DWM caption buttons to reappear");
    }

    [Fact]
    public async Task CloseButton_ShouldCloseWindow_WhenClicked()
    {
        Button? closeButton = FindFirst("TitleBarCloseButton").AsButton();

        closeButton.Should().NotBeNull("because CloseButton should be present in the main window title bar");
        closeButton.Click(moveMouse: false);

        await Wait(2, TestContext.Current.CancellationToken);

        Application
            ?.HasExited.Should()
            .BeTrue("because the main window should be closed after clicking the close button");
    }

    [Fact]
    public async Task MinimizeButton_ShouldHideWindow_WhenClicked()
    {
        Button? minimizeButton = FindFirst("TitleBarMinimizeButton").AsButton();

        minimizeButton
            .Should()
            .NotBeNull("because MinimizeButton should be present in the main window title bar");
        minimizeButton.Click(moveMouse: false);

        await Wait(2, TestContext.Current.CancellationToken);

        MainWindow
            ?.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault.Should()
            .Be(
                WindowVisualState.Minimized,
                "because the main window should be minimized after clicking the minimize button"
            );
    }

    [Fact]
    public async Task MaximizeButton_ShouldExpandWindow_WhenClicked()
    {
        Button? maximizeButton = FindFirst("TitleBarMaximizeButton").AsButton();

        maximizeButton
            .Should()
            .NotBeNull("because MaximizeButton should be present in the main window title bar");
        maximizeButton.Click(moveMouse: false);

        await Wait(2, TestContext.Current.CancellationToken);

        MainWindow
            ?.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault.Should()
            .Be(
                WindowVisualState.Maximized,
                "because the main window should be maximized after clicking the maximize button"
            );
    }

    [Fact]
    public async Task MaximizeButton_ShouldRestoreWindow_WhenClickedAgain()
    {
        Button? maximizeButton = FindFirst("TitleBarMaximizeButton").AsButton();

        maximizeButton
            .Should()
            .NotBeNull("because MaximizeButton should be present in the main window title bar");

        maximizeButton.Click(moveMouse: false);
        await Wait(1, TestContext.Current.CancellationToken);
        maximizeButton.Click(moveMouse: false);
        await Wait(1, TestContext.Current.CancellationToken);

        MainWindow
            ?.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault.Should()
            .Be(
                WindowVisualState.Normal,
                "because the second caption-button click should observe WM_SIZE and restore the window"
            );
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        internal static extern int GetWindowLong(IntPtr hwnd, int index);
    }
}
