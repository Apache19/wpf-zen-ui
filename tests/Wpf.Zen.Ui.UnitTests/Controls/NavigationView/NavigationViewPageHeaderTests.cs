// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

/// <summary>
/// Verifies page-owned header elements through WPF navigation and template changes.
/// </summary>
public class NavigationViewPageHeaderTests
{
    /// <summary>
    /// Verifies that the active page controls the header's binding and command context.
    /// </summary>
    [Fact]
    public void ActivePageHeaderKeepsPageBindingsAndChangesWithNavigation()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            ContentPresenter shellSlot = CreateShellHeader(navigationView);
            navigationView.MenuItems.Add(new Wpf.Zen.Ui.Controls.NavigationItem("First", typeof(HeaderPage)));
            navigationView.MenuItems.Add(new Wpf.Zen.Ui.Controls.NavigationItem("Second", typeof(PlainPage)));

            var window = new Window
            {
                DataContext = new object(),
                Content = navigationView,
                Width = 800,
                Height = 600,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                Assert.True(navigationView.Navigate(typeof(HeaderPage)));
                DrainDispatcher();

                var page = Assert.IsType<HeaderPage>(navigationView.CurrentLocation?.Page);
                var observedContexts = new List<object?>();
                page.HeaderButton.DataContextChanged += (_, args) => observedContexts.Add(args.NewValue);
                Assert.Same(
                    Wpf.Zen.Ui.Controls.NavigationView.GetPageHeaderContent(page),
                    shellSlot.Content
                );
                Assert.True(page.HeaderButton.IsLoaded);
                Assert.Equal("First action", page.HeaderText.Text);
                Assert.Same(page.Model.Command, page.HeaderButton.Command);
                ((IInvokeProvider)new ButtonAutomationPeer(page.HeaderButton)).Invoke();
                DrainDispatcher();
                Assert.Equal(1, ((TestCommand)page.Model.Command).ExecutionCount);

                var replacement = new HeaderModel("Updated action");
                page.DataContext = replacement;
                DrainDispatcher();
                Assert.Equal("Updated action", page.HeaderText.Text);
                Assert.Same(replacement.Command, page.HeaderButton.Command);

                navigationView.PaneDisplayMode =
                    Wpf.Zen.Ui.Controls.NavigationViewPaneDisplayMode.Top;
                DrainDispatcher();
                Assert.True(page.HeaderButton.IsLoaded);
                Assert.Same(replacement.Command, page.HeaderButton.Command);

                var replacementHeader = new TextBlock { Text = "Replacement" };
                replacementHeader.DataContextChanged += (_, args) => observedContexts.Add(args.NewValue);
                Wpf.Zen.Ui.Controls.NavigationView.SetPageHeaderContent(page, replacementHeader);
                DrainDispatcher();
                Assert.Same(replacementHeader, shellSlot.Content);
                Assert.True(replacementHeader.IsLoaded);
                Assert.False(page.HeaderButton.IsLoaded);

                Assert.True(navigationView.Navigate(typeof(PlainPage)));
                DrainDispatcher();
                Assert.Null(shellSlot.Content);
                Assert.False(replacementHeader.IsLoaded);
                Assert.DoesNotContain(window.DataContext, observedContexts);
                Assert.All(observedContexts, context => Assert.Same(replacement, context));
                Assert.Same(replacement, page.HeaderButton.DataContext);
                Assert.Same(replacement, replacementHeader.DataContext);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies retained bridges follow page changes, support reuse, and preserve explicit values.
    /// </summary>
    [Fact]
    public void ReusedHeaderRebindsToItsPageAndPreservesApplicationContexts()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            CreateShellHeader(navigationView);
            var window = new Window
            {
                DataContext = new object(),
                Content = navigationView,
                Width = 800,
                Height = 600,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                var first = new HeaderPage();
                var second = new PlainPage { DataContext = new HeaderModel("Second") };
                var header = Wpf.Zen.Ui.Controls.NavigationView.GetPageHeaderContent(first)!;
                Assert.True(navigationView.ReplaceContent(first));
                DrainDispatcher();
                var originalBridge = BindingOperations.GetBindingExpression(header, FrameworkElement.DataContextProperty);

                Assert.True(navigationView.ReplaceContent(second));
                DrainDispatcher();
                first.DataContext = new HeaderModel("Detached");
                DrainDispatcher();
                Assert.Same(first.DataContext, header.DataContext);

                Assert.True(navigationView.ReplaceContent(first));
                DrainDispatcher();
                var reusedBridge = BindingOperations.GetBindingExpression(header, FrameworkElement.DataContextProperty);
                Assert.Same(originalBridge, reusedBridge);

                Wpf.Zen.Ui.Controls.NavigationView.SetPageHeaderContent(first, null);
                DrainDispatcher();
                Wpf.Zen.Ui.Controls.NavigationView.SetPageHeaderContent(second, header);
                Assert.True(navigationView.ReplaceContent(second));
                DrainDispatcher();
                Assert.Same(second.DataContext, header.DataContext);

                second.DataContext = null;
                DrainDispatcher();
                Assert.Null(header.DataContext);
                Assert.Null(first.HeaderButton.Command);

                var explicitModel = new HeaderModel("Explicit");
                header.DataContext = explicitModel;
                Assert.True(navigationView.ReplaceContent(first));
                DrainDispatcher();
                Assert.True(navigationView.ReplaceContent(second));
                DrainDispatcher();
                Assert.Same(explicitModel, header.DataContext);

                var explicitSource = new Border { DataContext = explicitModel };
                var explicitBinding = new Binding(nameof(FrameworkElement.DataContext))
                {
                    Source = explicitSource,
                };
                var expression = header.SetBinding(FrameworkElement.DataContextProperty, explicitBinding);
                Assert.True(navigationView.ReplaceContent(first));
                DrainDispatcher();
                Assert.True(navigationView.ReplaceContent(second));
                DrainDispatcher();
                var retained = BindingOperations.GetBindingExpression(header, FrameworkElement.DataContextProperty);
                Assert.Same(expression, retained);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that a page can declare its contributed element as an attached property in XAML.
    /// </summary>
    [Fact]
    public void PageHeaderContentDeclaredInXamlLoadsAndBinds()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                      Title="Page title">
                    <ui:NavigationView.PageHeaderContent>
                        <TextBlock Text="{Binding Title}" />
                    </ui:NavigationView.PageHeaderContent>
                    <Grid />
                </Page>
                """;
            var page = Assert.IsType<Page>(XamlReader.Parse(xaml));
            page.DataContext = page;
            var contribution = Assert.IsType<TextBlock>(
                Wpf.Zen.Ui.Controls.NavigationView.GetPageHeaderContent(page)
            );
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            ContentPresenter shellSlot = CreateShellHeader(navigationView);
            var window = new Window
            {
                Content = navigationView,
                Width = 800,
                Height = 600,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                Assert.Same(contribution, shellSlot.Content);
                Assert.True(contribution.IsLoaded);
                Assert.Equal("Page title", contribution.Text);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    private static ContentPresenter CreateShellHeader(
        Wpf.Zen.Ui.Controls.NavigationView navigationView)
    {
        var contributionSlot = new ContentPresenter();
        BindingOperations.SetBinding(
            contributionSlot,
            ContentPresenter.ContentProperty,
            new Binding(nameof(Wpf.Zen.Ui.Controls.NavigationView.CurrentPageHeaderContent))
            {
                Source = navigationView,
            }
        );

        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "Shell header" });
        header.Children.Add(contributionSlot);
        navigationView.Header = header;
        return contributionSlot;
    }

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void DrainDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Supplies a bound header element for navigation tests.
    /// </summary>
    public sealed class HeaderPage : Page
    {
        /// <summary>
        /// Gets the initial page model.
        /// </summary>
        public HeaderModel Model { get; } = new("First action");

        /// <summary>
        /// Gets the bound header label.
        /// </summary>
        public TextBlock HeaderText { get; } = new();

        /// <summary>
        /// Gets the bound header command source.
        /// </summary>
        public Button HeaderButton { get; } = new();

        /// <summary>
        /// Initializes the page and its header contribution.
        /// </summary>
        public HeaderPage()
        {
            DataContext = Model;
            HeaderText.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderModel.Title)));
            HeaderButton.SetBinding(Button.CommandProperty, new Binding(nameof(HeaderModel.Command)));

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(HeaderText);
            header.Children.Add(HeaderButton);
            Wpf.Zen.Ui.Controls.NavigationView.SetPageHeaderContent(this, header);
            Content = new Border();
        }
    }

    /// <summary>
    /// A page without a header contribution.
    /// </summary>
    public sealed class PlainPage : Page { }

    /// <summary>
    /// Provides the data and command bound by a page's header element.
    /// </summary>
    /// <param name="title">The label presented in the header.</param>
    public sealed class HeaderModel(string title)
    {
        /// <summary>
        /// Gets the header label.
        /// </summary>
        public string Title { get; } = title;

        /// <summary>
        /// Gets the header action.
        /// </summary>
        public ICommand Command { get; } = new TestCommand();
    }

    private sealed class TestCommand : ICommand
    {
        public int ExecutionCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            ExecutionCount++;
        }
    }
}
