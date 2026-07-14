// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewTests
{
    [Fact]
    public void PaneDisplayMode_ReplacesVisualTemplateAndPreservesNavigationContent()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]);
            var leftTemplate = Assert.IsType<ControlTemplate>(resources["LeftNavigationViewTemplate"]);
            var fluentTemplate = Assert.IsType<ControlTemplate>(resources["FluentNavigationViewTemplate"]);
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = style,
                AutoSuggestBox = new AutoSuggestBox(),
                PaneHeader = new System.Windows.Controls.TextBlock { Text = "Header" },
                IsBackButtonVisible = NavigationViewBackButtonVisible.Visible,
                IsPaneToggleVisible = true,
            };

            Assert.NotSame(leftTemplate, fluentTemplate);
            Assert.Same(leftTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());

            var navigationPresenter = GetNavigationPresenter(navigationView);
            var page = new Border();
            navigationPresenter.JournalOwnership = System.Windows.Navigation.JournalOwnership.OwnsJournal;
            Assert.True(navigationView.ReplaceContent(page));
            DrainDispatcher();
            Assert.Same(page, navigationPresenter.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.Same(fluentTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.Null(fluentTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(fluentTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
            Assert.Equal(NavigationViewBackButtonVisible.Visible, navigationView.IsBackButtonVisible);
            Assert.True(navigationView.IsPaneToggleVisible);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.Same(leftTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(leftTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.Equal(navigationView.OpenPaneLength, GetPaneGrid(navigationView).Width);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
        });
    }

    [Fact]
    public void PaneDisplayMode_WhenLoaded_DoesNotNavigateAwayFromPreservedPage()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 1,
                Height = 1,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            Assert.True(navigationView.ApplyTemplate());
            var navigationPresenter = GetNavigationPresenter(navigationView);
            navigationPresenter.JournalOwnership = System.Windows.Navigation.JournalOwnership.OwnsJournal;
            var page = new NavigationAwarePage();

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                Assert.Equal(1, page.NavigatedToCount);
                Assert.Equal(0, page.NavigatedFromCount);

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
                Assert.True(navigationView.ApplyTemplate());
                DrainDispatcher();

                Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
                Assert.Same(page, navigationPresenter.Content);
                Assert.Equal(1, page.NavigatedToCount);
                Assert.Equal(0, page.NavigatedFromCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }

            Assert.Equal(1, page.NavigatedFromCount);
        });
    }

    private static NavigationViewContentPresenter GetNavigationPresenter(
        Wpf.Ui.Controls.NavigationView navigationView
    )
    {
        var host = Assert.IsType<ContentPresenter>(
            navigationView.Template.FindName("PART_NavigationViewContentPresenterHost", navigationView)
        );

        return Assert.IsType<NavigationViewContentPresenter>(host.Content);
    }

    private static Grid GetPaneGrid(Wpf.Ui.Controls.NavigationView navigationView) =>
        Assert.IsType<Grid>(navigationView.Template.FindName("PaneGrid", navigationView));

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void DrainDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        System.Windows.Threading.Dispatcher.PushFrame(frame);
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

    private sealed class NavigationAwarePage : Border, INavigationAware
    {
        public int NavigatedToCount { get; private set; }

        public int NavigatedFromCount { get; private set; }

        public Task OnNavigatedToAsync()
        {
            NavigatedToCount++;
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            NavigatedFromCount++;
            return Task.CompletedTask;
        }
    }
}
