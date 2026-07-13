// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewButtonTests
{
    [Fact]
    public void PaneDisplayModeChanged_IsRaisedWhenPaneDisplayModeChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var raisedCount = 0;

            navigationView.PaneDisplayModeChanged += (sender, args) =>
            {
                Assert.Same(navigationView, sender);
                Assert.Same(navigationView, args.Source);
                raisedCount++;
            };

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

            Assert.Equal(1, raisedCount);
            Assert.Equal(
                RoutingStrategy.Bubble,
                Wpf.Ui.Controls.NavigationView.PaneDisplayModeChangedEvent.RoutingStrategy
            );
            Assert.NotNull(typeof(INavigationView).GetEvent(nameof(INavigationView.PaneDisplayModeChanged)));
        });
    }

    [Fact]
    public void NavigationView_DoesNotExposeButtonStyleOverrides()
    {
        Assert.Null(typeof(Wpf.Ui.Controls.NavigationView).GetProperty("PaneButtonStyle"));
        Assert.Null(typeof(Wpf.Ui.Controls.NavigationView).GetProperty("TitleBarButtonStyle"));
        Assert.Null(typeof(Wpf.Ui.Controls.NavigationView).GetField("PaneButtonStyleProperty"));
        Assert.Null(typeof(Wpf.Ui.Controls.NavigationView).GetField("TitleBarButtonStyleProperty"));
        Assert.Null(typeof(INavigationView).GetProperty("PaneButtonStyle"));
        Assert.Null(typeof(INavigationView).GetProperty("TitleBarButtonStyle"));
    }

    [Fact]
    public void Control_HasAnIndependentButtonBaseTypeAndDefaultStyle()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var style = Assert.IsType<Style>(resources[typeof(NavigationViewButton)]);

            Assert.Equal(typeof(ButtonBase), typeof(NavigationViewButton).BaseType);
            Assert.Equal(typeof(NavigationViewButton), style.TargetType);

            var button = new NavigationViewButton { Style = style };
            Assert.Equal(NavigationViewButtonPlacement.None, button.Placement);
            Assert.True(button.ApplyTemplate());
            Assert.NotNull(button.Template);
        });
    }

    [Fact]
    public void OwnedButtons_SynchronizePaneStateAndExposeReadOnlyPlacement()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                IsPaneOpen = true,
            };

            var paneButton = GetButton(navigationView, "_paneBackButton");
            var titleBarButton = GetButton(navigationView, "_titleBarBackButton");

            Assert.True(NavigationViewButton.PaneDisplayModeProperty.ReadOnly);
            Assert.True(NavigationViewButton.PlacementProperty.ReadOnly);
            Assert.Equal(NavigationViewButtonPlacement.Pane, paneButton.Placement);
            Assert.Equal(NavigationViewButtonPlacement.TitleBar, titleBarButton.Placement);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, paneButton.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, titleBarButton.PaneDisplayMode);
            Assert.True(paneButton.IsPaneOpen);
            Assert.True(titleBarButton.IsPaneOpen);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;
            navigationView.IsPaneOpen = false;

            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, paneButton.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, titleBarButton.PaneDisplayMode);
            Assert.False(paneButton.IsPaneOpen);
            Assert.False(titleBarButton.IsPaneOpen);
        });
    }

    [Fact]
    public void AutoSuggestButton_UsesItsLocalStyleWithoutSpecialPlacement()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };

            Assert.True(navigationView.ApplyTemplate());

            var button = Assert.IsType<NavigationViewButton>(
                navigationView.Template.FindName("PART_AutoSuggestBoxSymbolButton", navigationView)
            );

            Assert.Equal(NavigationViewButtonPlacement.None, button.Placement);
            Assert.True(button.ApplyTemplate());
            Assert.NotNull(button.Template);
        });
    }

    private static NavigationViewButton GetButton(
        Wpf.Ui.Controls.NavigationView navigationView,
        string fieldName
    ) =>
        Assert.IsType<NavigationViewButton>(
            typeof(Wpf.Ui.Controls.NavigationView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(navigationView)
        );

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
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
