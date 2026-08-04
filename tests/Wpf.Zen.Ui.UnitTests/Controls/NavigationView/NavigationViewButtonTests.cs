// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewButtonTests
{
    [Fact]
    public void BackButtonPolicy_AutoTracksReadOnlyJournalStateAndTitleBarContent()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var titleBar = new Wpf.Zen.Ui.Controls.TitleBar();
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                TitleBar = titleBar,
                IsBackButtonVisible = NavigationViewBackButtonVisible.Auto,
                IsPaneToggleVisible = false,
            };
            navigationView.MenuItems.Add(
                new NavigationViewItem(
                    "First",
                    targetPageType: typeof(BackStackPageOne),
                    targetPageTag: "first"
                )
            );
            navigationView.MenuItems.Add(
                new NavigationViewItem(
                    "Second",
                    targetPageType: typeof(BackStackPageTwo),
                    targetPageTag: "second"
                )
            );

            var paneBackButton = GetButton(navigationView, "_paneBackButton");
            var titleBarBackButton = GetButton(navigationView, "_titleBarBackButton");

            Assert.True(Wpf.Zen.Ui.Controls.NavigationView.IsBackEnabledProperty.ReadOnly);
            Assert.Throws<InvalidOperationException>(
                () => navigationView.SetValue(
                    Wpf.Zen.Ui.Controls.NavigationView.IsBackEnabledProperty,
                    true
                )
            );
            Assert.False(navigationView.IsBackEnabled);
            Assert.Equal(Visibility.Collapsed, paneBackButton.Visibility);
            Assert.Equal(Visibility.Collapsed, titleBarBackButton.Visibility);
            Assert.Equal(TitleBarContentState.Collapsed, titleBar.LeftContentState);

            var window = new Window
            {
                Content = navigationView,
                Width = 800,
                Height = 600,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(navigationView.Navigate(typeof(BackStackPageOne)));
                Assert.False(navigationView.IsBackEnabled);
                Assert.Equal(Visibility.Collapsed, titleBarBackButton.Visibility);
                Assert.Equal(TitleBarContentState.Collapsed, titleBar.LeftContentState);

                Assert.True(navigationView.Navigate(typeof(BackStackPageTwo)));
                Assert.True(navigationView.IsBackEnabled);
                Assert.True(titleBarBackButton.IsEnabled);
                Assert.Equal(Visibility.Visible, paneBackButton.Visibility);
                Assert.Equal(Visibility.Visible, titleBarBackButton.Visibility);
                Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);

                navigationView.ClearJournal();
                Assert.False(navigationView.IsBackEnabled);
                Assert.False(titleBarBackButton.IsEnabled);
                Assert.Equal(Visibility.Collapsed, paneBackButton.Visibility);
                Assert.Equal(Visibility.Collapsed, titleBarBackButton.Visibility);
                Assert.Equal(TitleBarContentState.Collapsed, titleBar.LeftContentState);

                navigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
                Assert.Equal(Visibility.Visible, titleBarBackButton.Visibility);
                Assert.Equal(TitleBarContentState.Visible, titleBar.LeftContentState);
                Assert.False(titleBarBackButton.IsEnabled);

                navigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
                Assert.Equal(Visibility.Collapsed, titleBarBackButton.Visibility);
                Assert.Equal(TitleBarContentState.Empty, titleBar.LeftContentState);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PaneDisplayModeChanged_IsRaisedWhenPaneDisplayModeChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
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
                Wpf.Zen.Ui.Controls.NavigationView.PaneDisplayModeChangedEvent.RoutingStrategy
            );
            Assert.NotNull(typeof(INavigationView).GetEvent(nameof(INavigationView.PaneDisplayModeChanged)));
        });
    }

    [Fact]
    public void NavigationView_DoesNotExposeButtonStyleOverrides()
    {
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.NavigationView).GetProperty("PaneButtonStyle"));
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.NavigationView).GetProperty("TitleBarButtonStyle"));
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.NavigationView).GetField("PaneButtonStyleProperty"));
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.NavigationView).GetField("TitleBarButtonStyleProperty"));
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

            Assert.Equal(typeof(object), NavigationViewButton.IconProperty.PropertyType);
            Assert.Null(typeof(NavigationViewButton).GetProperty("IconTemplate"));
            Assert.Null(typeof(NavigationViewButton).GetField("IconTemplateProperty"));
            // Navigation resources provide the native data-template path used by reusable symbol
            // values. The template creates visuals; Icon itself remains an unrestricted object.
            Assert.True(resources.Contains(new DataTemplateKey(typeof(FluentSystemIcon))));

            var button = new NavigationViewButton { Style = style };
            Assert.Equal(NavigationViewButtonPlacement.None, button.Placement);
            var arbitraryIcon = new Border();
            button.Icon = arbitraryIcon;
            Assert.Same(arbitraryIcon, button.Icon);
            Assert.True(button.ApplyTemplate());
            Assert.NotNull(button.Template);
        });
    }

    [Fact]
    public void OwnedButtons_SynchronizePaneStateAndExposeReadOnlyPlacement()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                IsPaneOpen = true,
            };

            var paneButton = GetButton(navigationView, "_paneBackButton");
            var titleBarButton = GetButton(navigationView, "_titleBarBackButton");

            Assert.True(NavigationViewButton.PaneDisplayModeProperty.ReadOnly);
            Assert.True(NavigationViewButton.PlacementProperty.ReadOnly);
            Assert.True(NavigationViewButton.PaneTitleProperty.ReadOnly);
            Assert.True(NavigationViewButton.RoleProperty.ReadOnly);
            Assert.Equal(NavigationViewButtonPlacement.Pane, paneButton.Placement);
            Assert.Equal(NavigationViewButtonPlacement.TitleBar, titleBarButton.Placement);
            Assert.Equal(NavigationViewButtonRole.Back, paneButton.Role);
            Assert.Equal(NavigationViewButtonRole.Back, titleBarButton.Role);
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
    public void NestedNavigationView_PaneEventsRemainRoutedButButtonStateStaysWithItsOwner()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewStyle = Assert.IsType<Style>(
                resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]
            );
            var innerNavigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = navigationViewStyle,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var outerNavigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = navigationViewStyle,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                IsPaneOpen = true,
                PaneHeader = innerNavigationView,
            };
            var outerButton = GetButton(outerNavigationView, "_paneToggleButton");
            var innerButton = GetButton(innerNavigationView, "_paneToggleButton");
            var bubbledEventCount = 0;

            outerNavigationView.PaneClosed += (sender, args) =>
            {
                if (!ReferenceEquals(args.OriginalSource, innerNavigationView))
                {
                    return;
                }

                Assert.Same(outerNavigationView, sender);
                bubbledEventCount++;
            };

            var window = new Window
            {
                Content = outerNavigationView,
                Width = 800,
                Height = 600,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                innerNavigationView.IsPaneOpen = false;

                Assert.Equal(1, bubbledEventCount);
                Assert.Equal(NavigationViewPaneDisplayMode.Top, outerButton.PaneDisplayMode);
                Assert.True(outerButton.IsPaneOpen);
                Assert.Equal(NavigationViewPaneDisplayMode.Left, innerButton.PaneDisplayMode);
                Assert.False(innerButton.IsPaneOpen);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PaneTitle_IsSynchronizedWithoutTakingOwnershipOfButtonContent()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneTitle = "_Navigation",
                IsPaneOpen = true,
            };

            var toggleButton = GetButton(navigationView, "_paneToggleButton");
            var backButton = GetButton(navigationView, "_paneBackButton");

            Assert.Equal(NavigationViewButtonRole.PaneToggle, toggleButton.Role);
            Assert.Equal("_Navigation", toggleButton.PaneTitle);
            Assert.Equal("_Navigation", backButton.PaneTitle);
            Assert.Null(toggleButton.Content);
            Assert.Null(
                typeof(Wpf.Zen.Ui.Controls.NavigationView).GetField(
                    "_paneTitle",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
            );

            toggleButton.Content = "Application content";
            navigationView.PaneTitle = "_Updated";
            navigationView.IsPaneOpen = false;
            navigationView.IsPaneOpen = true;

            Assert.Equal("_Updated", toggleButton.PaneTitle);
            Assert.Equal("_Updated", backButton.PaneTitle);
            Assert.Equal("Application content", toggleButton.Content);
        });
    }

    [Fact]
    public void DefaultTemplate_PresentsPaneTitleAsAccessTextOnlyWhenContentIsNull()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneTitle = "_Navigation",
                IsPaneOpen = true,
            };
            var toggleButton = GetButton(navigationView, "_paneToggleButton");
            var backButton = GetButton(navigationView, "_paneBackButton");
            var style = Assert.IsType<Style>(resources[typeof(NavigationViewButton)]);
            toggleButton.Style = style;
            backButton.Style = style;

            Assert.True(toggleButton.ApplyTemplate());

            var contentPresenter = Assert.IsType<ContentPresenter>(
                toggleButton.Template.FindName("contentPresenter", toggleButton)
            );
            var paneTitlePresenter = Assert.IsType<ContentPresenter>(
                toggleButton.Template.FindName("paneTitlePresenter", toggleButton)
            );
            var iconPresenter = Assert.IsType<ContentPresenter>(
                toggleButton.Template.FindName("iconPresenter", toggleButton)
            );

            Assert.Equal("_Navigation", paneTitlePresenter.Content);
            Assert.True(paneTitlePresenter.RecognizesAccessKey);
            Assert.Equal(Visibility.Collapsed, contentPresenter.Visibility);
            Assert.Equal(Visibility.Visible, paneTitlePresenter.Visibility);
            Assert.Equal(
                SymbolRegular.LineHorizontal320,
                Assert.IsType<FluentSystemIcon>(toggleButton.Icon).Symbol
            );
            Assert.Same(toggleButton.Icon, iconPresenter.Content);
            Assert.Equal(
                SymbolRegular.ArrowLeft48,
                Assert.IsType<FluentSystemIcon>(backButton.Icon).Symbol
            );

            Assert.True(iconPresenter.ApplyTemplate());
            Assert.IsType<SymbolIcon>(VisualTreeHelper.GetChild(iconPresenter, 0));

            navigationView.PaneTitle = null;
            Assert.Equal(Visibility.Collapsed, paneTitlePresenter.Visibility);

            navigationView.PaneTitle = "_Navigation";
            toggleButton.Content = "Application content";
            var arbitraryIcon = new Border();
            toggleButton.Icon = arbitraryIcon;

            Assert.Equal(Visibility.Visible, contentPresenter.Visibility);
            Assert.Equal(Visibility.Collapsed, paneTitlePresenter.Visibility);
            Assert.Equal("Application content", contentPresenter.Content);
            Assert.Same(arbitraryIcon, iconPresenter.Content);

            // Changing Icon must make ContentPresenter reselect its native UIElement template. If
            // the role-specific SymbolRegular template escapes its local resource scope or is set
            // explicitly, the Border is instead wrapped in a SymbolIcon and disappears visually.
            toggleButton.Measure(new Size(100, 40));
            toggleButton.Arrange(new Rect(0, 0, 100, 40));
            Assert.Same(arbitraryIcon, VisualTreeHelper.GetChild(iconPresenter, 0));
        });
    }

    [Fact]
    public void AutoSuggestButton_UsesItsLocalStyleWithoutSpecialPlacement()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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

    [Fact]
    public void AutoSuggestBoxSymbolButton_UsesIndependentReusableSymbolData()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };

            var autoSuggestIcon = new Border();
            var autoSuggestBox = new AutoSuggestBox { Icon = autoSuggestIcon };

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                AutoSuggestBox = autoSuggestBox,
            };

            Assert.True(navigationView.ApplyTemplate());

            var button = Assert.IsType<NavigationViewButton>(
                navigationView.Template.FindName(
                    "PART_AutoSuggestBoxSymbolButton",
                    navigationView
                )
            );

            Assert.Same(autoSuggestIcon, autoSuggestBox.Icon);
            Assert.Equal(
                SymbolRegular.Search24,
                Assert.IsType<FluentSystemIcon>(button.Icon).Symbol
            );

            // The compact command represents the search role; it must not try to clone or reparent
            // arbitrary visual content owned by AutoSuggestBox.
            autoSuggestBox.Icon = new System.Windows.Controls.TextBlock { Text = "Replacement" };
            Assert.Equal(
                SymbolRegular.Search24,
                Assert.IsType<FluentSystemIcon>(button.Icon).Symbol
            );
        });
    }

    [Fact]
    public void AutoSuggestBoxSymbolButton_Icon_HasDefaultSearchIconWhenNoAutoSuggestBox()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };

            Assert.True(navigationView.ApplyTemplate());

            var button = Assert.IsType<NavigationViewButton>(
                navigationView.Template.FindName(
                    "PART_AutoSuggestBoxSymbolButton",
                    navigationView
                )
            );

            Assert.Equal(
                SymbolRegular.Search24,
                Assert.IsType<FluentSystemIcon>(button.Icon).Symbol
            );
        });
    }

    private static NavigationViewButton GetButton(
        Wpf.Zen.Ui.Controls.NavigationView navigationView,
        string fieldName
    ) =>
        Assert.IsType<NavigationViewButton>(
            typeof(Wpf.Zen.Ui.Controls.NavigationView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(navigationView)
        );

    public sealed class BackStackPageOne : Border
    { }

    public sealed class BackStackPageTwo : Border
    { }

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void DrainDispatcher() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            static () => { },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle
        );

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
