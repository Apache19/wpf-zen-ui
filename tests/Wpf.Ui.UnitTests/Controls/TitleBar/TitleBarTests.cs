// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Win32;

namespace Wpf.Ui.UnitTests.Controls.TitleBar;

public class TitleBarTests
{
    [Fact]
    public void RefactoredPublicSurface_UsesSemanticRolesAndNativeBorderSwitch()
    {
        RunOnStaThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow();

            Assert.True(window.UseNativeWindowBorder);
            Assert.Equal(
                true,
                Wpf.Ui.Controls.FluentWindow.UseNativeWindowBorderProperty.DefaultMetadata.DefaultValue
            );
            Assert.Null(typeof(Wpf.Ui.Controls.FluentWindow).GetProperty("OsVersion"));
            Assert.Null(typeof(Wpf.Ui.Controls.FluentWindow).GetEvent("FirstShown"));
            Assert.Null(typeof(Wpf.Ui.Controls.TitleBar).GetMethod("GetElementIdentity"));
            Assert.Null(typeof(TitleBarButton).GetProperty("IsMouseActualOver"));
        });
    }

    [Fact]
    public void UseNativeWindowBorder_RecoercesVisualBorderAtRuntime()
    {
        RunOnStaThread(() =>
        {
            var window = new Wpf.Ui.Controls.FluentWindow();

            // OnSourceInitialized performs this first coercion after the HWND and operating-system strategy are
            // available. Reproduce that lifecycle boundary without creating a native test window.
            window.CoerceValue(Wpf.Ui.Controls.FluentWindow.BorderThicknessProperty);
            var expectedNativeBorder = FluentWindowNonClientArea.CalculateVisualBorderThickness(
                FluentWindowNonClientArea.SelectFrameStrategy(
                    Utilities.IsWin_11_Or_Greater,
                    Utilities.IsWin_10_Or_Greater,
                    true
                ),
                WindowState.Normal
            );
            Assert.Equal(expectedNativeBorder, window.BorderThickness);

            window.UseNativeWindowBorder = false;
            var expectedClientBorder = FluentWindowNonClientArea.CalculateVisualBorderThickness(
                FluentWindowNonClientArea.SelectFrameStrategy(
                    Utilities.IsWin_11_Or_Greater,
                    Utilities.IsWin_10_Or_Greater,
                    false
                ),
                WindowState.Normal
            );
            Assert.Equal(expectedClientBorder, window.BorderThickness);

            window.UseNativeWindowBorder = true;
            Assert.Equal(expectedNativeBorder, window.BorderThickness);
        });
    }

    [Fact]
    public void HitTestRole_IsInheritedAndCanBeOverriddenByInteractiveContent()
    {
        RunOnStaThread(() =>
        {
            var captionContainer = new Grid();
            var inheritedChild = new Border();
            var interactiveChild = new Border();
            captionContainer.Children.Add(inheritedChild);
            captionContainer.Children.Add(interactiveChild);

            Wpf.Ui.Controls.TitleBar.SetHitTestRole(captionContainer, TitleBarHitTestRole.Caption);
            Wpf.Ui.Controls.TitleBar.SetHitTestRole(interactiveChild, TitleBarHitTestRole.Client);

            Assert.Equal(
                TitleBarHitTestRole.Caption,
                Wpf.Ui.Controls.TitleBar.GetHitTestRole(inheritedChild)
            );
            Assert.Equal(
                TitleBarHitTestRole.Client,
                Wpf.Ui.Controls.TitleBar.GetHitTestRole(interactiveChild)
            );
        });
    }

    [Fact]
    public void TitleBarButton_PointerStateIsReadOnly()
    {
        RunOnStaThread(() =>
        {
            var button = new TitleBarButton();

            Assert.True(TitleBarButton.IsPointerOverProperty.ReadOnly);
            Assert.False(button.IsPointerOver);
            Assert.Throws<InvalidOperationException>(() =>
                button.SetValue(TitleBarButton.IsPointerOverProperty, true)
            );
        });
    }

    [Fact]
    public void DefaultTemplate_AssignsNativeRolesToEveryCaptionButton()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var titleBar = new Wpf.Ui.Controls.TitleBar
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.TitleBar)]),
            };

            Assert.True(titleBar.ApplyTemplate());

            Assert.Equal(
                TitleBarHitTestRole.MinimizeButton,
                Wpf.Ui.Controls.TitleBar.GetHitTestRole(FindButton(titleBar, "MinimizeButton"))
            );
            Assert.Equal(
                TitleBarHitTestRole.MaximizeButton,
                Wpf.Ui.Controls.TitleBar.GetHitTestRole(FindButton(titleBar, "MaximizeButton"))
            );
            Assert.Equal(
                TitleBarHitTestRole.CloseButton,
                Wpf.Ui.Controls.TitleBar.GetHitTestRole(FindButton(titleBar, "CloseButton"))
            );
        });
    }

    [Fact]
    public void LeftContentState_IsReadOnlyAndTracksDirectElementVisibility()
    {
        RunOnStaThread(() =>
        {
            var titleBar = new Wpf.Ui.Controls.TitleBar();
            var firstElement = new Border { Visibility = Visibility.Hidden };

            Assert.True(Wpf.Ui.Controls.TitleBar.LeftContentStateProperty.ReadOnly);
            Assert.Equal(TitleBarContentState.Empty, titleBar.LeftContentState);
            Assert.Throws<InvalidOperationException>(() =>
                titleBar.SetValue(
                    Wpf.Ui.Controls.TitleBar.LeftContentStateProperty,
                    TitleBarContentState.Visible
                )
            );

            titleBar.LeftContent = firstElement;
            Assert.Equal(TitleBarContentState.Hidden, titleBar.LeftContentState);

            firstElement.Visibility = Visibility.Collapsed;
            Assert.Equal(TitleBarContentState.Collapsed, titleBar.LeftContentState);

            firstElement.Visibility = Visibility.Visible;
            Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);

            // A data object has no direct Visibility at the property boundary. Its data template is
            // responsible for presentation, so the title bar must treat the assigned value as visible.
            titleBar.LeftContent = "Application content";
            Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);

            // Replacing the slot must also replace the private visibility binding. Mutating the old
            // element can no longer affect the state of the new content.
            firstElement.Visibility = Visibility.Hidden;
            Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);

            titleBar.LeftContent = null;
            Assert.Equal(TitleBarContentState.Empty, titleBar.LeftContentState);
        });
    }

    [Fact]
    public void DefaultTemplate_UsesOnlyVisibleLeftContentForLeadingLayout()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var titleBar = new Wpf.Ui.Controls.TitleBar
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.TitleBar)]),
                Title = "Window title",
            };

            Assert.True(titleBar.ApplyTemplate());

            var leftContentPresenter = Assert.IsType<ContentPresenter>(
                titleBar.Template.FindName("LeftContentPresenter", titleBar)
            );
            var iconGrid = Assert.IsType<Grid>(titleBar.Template.FindName("IconGrid", titleBar));

            Assert.Equal(Visibility.Collapsed, leftContentPresenter.Visibility);
            Assert.Equal(titleBar.Padding, iconGrid.Margin);

            var element = new Border { Visibility = Visibility.Hidden };
            titleBar.LeftContent = element;
            Assert.Equal(TitleBarContentState.Hidden, titleBar.LeftContentState);
            Assert.Equal(Visibility.Collapsed, leftContentPresenter.Visibility);
            Assert.Equal(titleBar.Padding, iconGrid.Margin);

            element.Visibility = Visibility.Visible;
            Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);
            Assert.Equal(Visibility.Visible, leftContentPresenter.Visibility);
            Assert.Equal(
                new Thickness(2D, titleBar.Padding.Top, titleBar.Padding.Right, titleBar.Padding.Bottom),
                iconGrid.Margin
            );

            element.Visibility = Visibility.Collapsed;
            Assert.Equal(TitleBarContentState.Collapsed, titleBar.LeftContentState);
            Assert.Equal(Visibility.Collapsed, leftContentPresenter.Visibility);
            Assert.Equal(titleBar.Padding, iconGrid.Margin);
        });
    }

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static TitleBarButton FindButton(Wpf.Ui.Controls.TitleBar titleBar, string name)
    {
        return Assert.IsType<TitleBarButton>(titleBar.Template.FindName(name, titleBar));
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
