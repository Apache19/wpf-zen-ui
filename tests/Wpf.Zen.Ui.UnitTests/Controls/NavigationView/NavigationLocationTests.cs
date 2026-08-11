// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationLocationTests
{
    [Fact]
    public void IncompleteMap_PreservesResolvableSuffix()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add(new NavigationMapNode("b", "B", parentKey: "missing"));
            navigationView.NavigationMap.Add(new NavigationMapNode("c", "C", parentKey: "b"));
            navigationView.NavigationMap.Add(
                new NavigationMapNode(
                    "d",
                    "D",
                    new NavigationTarget(typeof(DestinationPage), "d"),
                    "c")
            );

            Assert.True(navigationView.Navigate(typeof(DestinationPage)));
            Assert.Equal(new[] { "B", "C", "D" }, navigationView.Breadcrumbs.Select(x => x.Content));
        });
    }

    [Fact]
    public void ExplicitRequestPath_HasPriorityOverMap()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add(
                new NavigationMapNode(
                    "mapped",
                    "Mapped",
                    new NavigationTarget(typeof(DestinationPage), "mapped"))
            );
            NavigationRequest request = NavigationRequest
                .To<DestinationPage>()
                .WithBreadcrumbs(
                    new BreadcrumbSegment("a", "A"),
                    new BreadcrumbSegment("b", "B"),
                    new BreadcrumbSegment(
                        "d",
                        "D",
                        NavigationTarget.ForPage<DestinationPage>())
                );

            NavigationResult result = ((INavigationHost)navigationView).Navigate(request);

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { "A", "B", "D" }, navigationView.Breadcrumbs.Select(x => x.Content));
        });
    }

    [Fact]
    public void MapMutation_ReparentsCurrentLocationWithoutAddingHistory()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add(new NavigationMapNode("a", "A"));
            navigationView.NavigationMap.Add(new NavigationMapNode("b", "B"));
            var destination = new NavigationMapNode(
                "d",
                "D",
                new NavigationTarget(typeof(DestinationPage), "d"),
                "a");
            navigationView.NavigationMap.Add(destination);

            Assert.True(navigationView.Navigate(typeof(DestinationPage)));
            Assert.Equal(new[] { "A", "D" }, navigationView.Breadcrumbs.Select(x => x.Content));
            Assert.False(navigationView.CanGoBack);

            destination.ParentKey = "b";

            Assert.Equal(new[] { "B", "D" }, navigationView.Breadcrumbs.Select(x => x.Content));
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void DynamicPageSource_UpdatesLeafAndUsesMapParent()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add(
                new NavigationMapNode(
                    "articles",
                    "Articles",
                    NavigationTarget.ForPage<ArticleListPage>())
            );

            Assert.True(navigationView.Navigate(typeof(DynamicArticlePage)));
            var page = Assert.IsType<DynamicArticlePage>(navigationView.CurrentLocation?.Page);
            Assert.Equal(new[] { "Articles", "Initial title" }, navigationView.Breadcrumbs.Select(x => x.Content));

            page.ChangeTitle("Updated title");

            Assert.Equal(new[] { "Articles", "Updated title" }, navigationView.Breadcrumbs.Select(x => x.Content));
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void AnonymousPage_DoesNotInferParentFromJournal()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();

            Assert.True(navigationView.Navigate(typeof(ArticleListPage)));
            Assert.True(navigationView.Navigate(typeof(DestinationPage)));

            BreadcrumbSegment leaf = Assert.Single(navigationView.Breadcrumbs);
            Assert.Equal(nameof(DestinationPage), leaf.Content);
        });
    }

    [Fact]
    public void NavigationMap_ConvenienceAddAndBatchUpdate_ResolveOnlyFinalPath()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Update(map =>
            {
                _ = map.Add("a", "A");
                _ = map.Add("b", "B");
                _ = map.Add<DestinationPage>("d", "D", "a");
            });

            Assert.True(navigationView.Navigate(typeof(DestinationPage)));
            Assert.Equal(new[] { "A", "D" }, navigationView.Breadcrumbs.Select(x => x.Content));

            int locationChanges = 0;
            int collectionResets = 0;
            navigationView.CurrentLocationChanged += (_, _) => locationChanges++;
            ((INotifyCollectionChanged)navigationView.Breadcrumbs).CollectionChanged += (_, args) =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action);
                collectionResets++;
            };

            navigationView.NavigationMap.Update(map =>
            {
                Assert.True(map.TryGetNode("d", out NavigationMapNode destination));
                destination.ParentKey = "b";
                destination.Content = "Renamed";
            });

            Assert.Equal(new[] { "B", "Renamed" }, navigationView.Breadcrumbs.Select(x => x.Content));
            Assert.Equal(1, locationChanges);
            Assert.Equal(1, collectionResets);
        });
    }

    [Fact]
    public void Journal_BackAndForwardRestoreOccurrences_AndNewNavigationClearsForwardHistory()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Update(map =>
            {
                _ = map.Add<DestinationPage>("destination", "Destination");
                _ = map.Add<ArticleListPage>("articles", "Articles");
                _ = map.Add<ThirdPage>("third", "Third");
            });

            Assert.True(navigationView.Navigate(typeof(DestinationPage)));
            Assert.True(navigationView.Navigate(typeof(ArticleListPage)));
            Assert.True(navigationView.GoBack());
            Assert.True(navigationView.CanGoForward);
            Assert.Equal("Destination", Assert.Single(navigationView.Breadcrumbs).Content);

            Assert.True(navigationView.GoForward());
            Assert.False(navigationView.CanGoForward);
            Assert.Equal("Articles", Assert.Single(navigationView.Breadcrumbs).Content);

            Assert.True(navigationView.GoBack());
            Assert.True(navigationView.Navigate(typeof(ThirdPage)));
            Assert.False(navigationView.CanGoForward);
            Assert.False(navigationView.GoForward());
        });
    }

    [Fact]
    public void NavigationService_NavigatesBreadcrumbAndRejectsStructuralSegment()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            using INavigationScope scope = NavigationScope.Attach(navigationView);
            var structural = new BreadcrumbSegment("section", "Section");
            var destination = new BreadcrumbSegment(
                "destination",
                "Destination",
                NavigationTarget.ForPage<DestinationPage>()
            );

            Assert.Equal(NavigationResultStatus.Rejected, scope.Navigation.Navigate(structural).Status);
            Assert.True(scope.Navigation.Navigate(destination).Succeeded);
            Assert.IsType<DestinationPage>(navigationView.CurrentLocation?.Page);
            Assert.True(scope.Navigation.Navigate(typeof(ArticleListPage)).Succeeded);
            Assert.True(scope.Navigation.GoBack().Succeeded);
            Assert.True(scope.Navigation.CanGoForward);
            Assert.True(scope.Navigation.GoForward().Succeeded);
            Assert.IsType<ArticleListPage>(navigationView.CurrentLocation?.Page);
        });
    }

    [Fact]
    public void ExplicitPath_RejectsNullSegmentsAtRequestBoundary()
    {
        NavigationRequest request = NavigationRequest.To<DestinationPage>();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => request.WithBreadcrumbs(new BreadcrumbSegment("a", "A"), null!)
        );

        Assert.Contains("null segments", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocationDescriptor_SnapshotsExplicitPath()
    {
        var original = new List<BreadcrumbSegment>
        {
            new("a", "A"),
        };

        var descriptor = new NavigationLocationDescriptor(breadcrumbs: original);
        original.Add(new BreadcrumbSegment("b", "B"));

        Assert.Single(descriptor.Breadcrumbs!);
        _ = Assert.Throws<ArgumentException>(
            () => new NavigationLocationDescriptor(breadcrumbs: Array.Empty<BreadcrumbSegment>())
        );
    }

    [Fact]
    public void SameTarget_WithDifferentDataContext_IsANewOccurrence()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var first = new object();
            var second = new object();

            Assert.True(navigationView.Navigate(typeof(DestinationPage), first));
            Assert.True(navigationView.Navigate(typeof(DestinationPage), second));
            Assert.True(navigationView.CanGoBack);
            Assert.Same(second, navigationView.CurrentLocation?.Page is FrameworkElement page
                ? page.DataContext
                : null);
        });
    }

    [Fact]
    public void NavigationMap_ParsesDirectNodeCollectionFromXaml()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <ui:NavigationMap
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <ui:NavigationMapNode Key="root" Content="Root" />
                    <ui:NavigationMapNode
                        Key="destination"
                        ParentKey="root"
                        Content="Destination"
                        IsBreadcrumbVisible="False"
                        PageType="{x:Type Border}" />
                </ui:NavigationMap>
                """;

            var map = Assert.IsType<NavigationMap>(XamlReader.Parse(xaml));

            Assert.Equal(2, map.Count);
            Assert.True(map.TryGetNode("destination", out NavigationMapNode destination));
            Assert.Equal("root", destination.ParentKey);
            Assert.Equal(typeof(Border), destination.PageType);
            Assert.False(destination.IsBreadcrumbVisible);
        });
    }

    [Fact]
    public void HiddenMapNode_IsOmittedWithoutLeavingMapOrBreakingDescendantResolution()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            NavigationMapNode category = navigationView.NavigationMap.Add<DestinationPage>(
                "category",
                "Category",
                isBreadcrumbVisible: false
            );
            navigationView.NavigationMap.Add<ArticleListPage>("article", "Article", "category");

            Assert.True(navigationView.Navigate(typeof(DestinationPage)));
            Assert.Empty(navigationView.Breadcrumbs);
            Assert.Contains(category, navigationView.NavigationMap);
            Assert.False(navigationView.CanGoBack);

            category.IsBreadcrumbVisible = true;

            Assert.Equal("Category", Assert.Single(navigationView.Breadcrumbs).Content);
            Assert.False(navigationView.CanGoBack);

            category.IsBreadcrumbVisible = false;
            Assert.True(navigationView.Navigate(typeof(ArticleListPage)));
            Assert.Equal("Article", Assert.Single(navigationView.Breadcrumbs).Content);
        });
    }

    [Fact]
    public void PageXaml_DeclaresBindableCompleteBreadcrumbPath()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <Border
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027">
                    <ui:NavigationView.BreadcrumbPath>
                        <ui:BreadcrumbPath>
                            <ui:BreadcrumbPathSegment
                                Key="articles"
                                Content="Articles"
                                RouteKey="articles" />
                            <ui:BreadcrumbPathSegment
                                Key="article"
                                Content="{Binding Title}" />
                        </ui:BreadcrumbPath>
                    </ui:NavigationView.BreadcrumbPath>
                </Border>
                """;
            var page = Assert.IsType<Border>(XamlReader.Parse(xaml));
            var viewModel = new ArticleLocationViewModel("Initial title");
            page.DataContext = viewModel;
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            using INavigationScope scope = NavigationScope.Attach(
                navigationView,
                new ExistingPageProvider(page)
            );

            Assert.True(scope.Navigation.Navigate(typeof(Border)).Succeeded);
            Dispatcher.CurrentDispatcher.Invoke(
                static () => { },
                DispatcherPriority.DataBind
            );
            Assert.Equal(
                new[] { "Articles", "Initial title" },
                navigationView.Breadcrumbs.Select(item => item.Content)
            );

            viewModel.Title = "Updated title";
            Dispatcher.CurrentDispatcher.Invoke(
                static () => { },
                DispatcherPriority.DataBind
            );

            Assert.Equal("Updated title", navigationView.Breadcrumbs[^1].Content);
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void BreadcrumbPath_ClonePreservesDerivedTypeAndSegments()
    {
        WpfTestThread.Run(() =>
        {
            var path = new BreadcrumbPath
            {
                new BreadcrumbPathSegment
                {
                    Key = "articles",
                    Content = "Articles",
                    RouteKey = "articles",
                },
            };

            var clone = Assert.IsType<BreadcrumbPath>(path.Clone());

            BreadcrumbPathSegment segment = Assert.Single(clone);
            Assert.Equal("articles", segment.Key);
            Assert.Equal("Articles", segment.Content);
            Assert.Equal("articles", segment.RouteKey);
            Assert.NotSame(path[0], segment);
        });
    }

    [Fact]
    public void PageXaml_DeclaresMapParentKey()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <Border
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                    ui:NavigationView.BreadcrumbContent="Preview"
                    ui:NavigationView.BreadcrumbParentKey="imports" />
                """;
            var page = Assert.IsType<Border>(XamlReader.Parse(xaml));
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add("projects", "Projects");
            navigationView.NavigationMap.Add("imports", "Imports", parentKey: "projects");
            using INavigationScope scope = NavigationScope.Attach(
                navigationView,
                new ExistingPageProvider(page)
            );

            Assert.True(scope.Navigation.Navigate(typeof(Border)).Succeeded);
            Assert.Equal(
                new[] { "Projects", "Imports", "Preview" },
                navigationView.Breadcrumbs.Select(item => item.Content)
            );
            Assert.False(navigationView.Breadcrumbs[0].IsNavigable);
            Assert.False(navigationView.Breadcrumbs[1].IsNavigable);
        });
    }

    [Fact]
    public void BoundPageParentKey_ReparentsCurrentLocationWithoutAddingHistory()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <Border
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                    ui:NavigationView.BreadcrumbContent="Preview"
                    ui:NavigationView.BreadcrumbParentKey="{Binding ParentKey}" />
                """;
            var page = Assert.IsType<Border>(XamlReader.Parse(xaml));
            var viewModel = new ParentLocationViewModel("first");
            page.DataContext = viewModel;
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            navigationView.NavigationMap.Add("first", "First");
            navigationView.NavigationMap.Add("second", "Second");
            using INavigationScope scope = NavigationScope.Attach(
                navigationView,
                new ExistingPageProvider(page)
            );

            Assert.True(scope.Navigation.Navigate(typeof(Border)).Succeeded);
            Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.DataBind);
            Assert.Equal(new[] { "First", "Preview" }, navigationView.Breadcrumbs.Select(x => x.Content));

            viewModel.ParentKey = "second";
            Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.DataBind);

            Assert.Equal(new[] { "Second", "Preview" }, navigationView.Breadcrumbs.Select(x => x.Content));
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void DisposingScope_ClearsNavigationParentFromExternallyOwnedPage()
    {
        WpfTestThread.Run(() =>
        {
            var page = new Border();
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            INavigationScope scope = NavigationScope.Attach(
                navigationView,
                new ExistingPageProvider(page)
            );

            Assert.True(scope.Navigation.Navigate(typeof(Border)).Succeeded);
            Assert.Same(navigationView, Wpf.Zen.Ui.Controls.NavigationView.GetNavigationParent(page));

            scope.Dispose();

            Assert.Null(Wpf.Zen.Ui.Controls.NavigationView.GetNavigationParent(page));
        });
    }

    private sealed class DestinationPage : Border;

    private sealed class ArticleListPage : Border;

    private sealed class ThirdPage : Border;

    private sealed class DynamicArticlePage : Border, INavigationLocationSource
    {
        private string _title = "Initial title";

        public event EventHandler? LocationChanged;

        public NavigationLocationDescriptor GetNavigationLocation(NavigationLocationContext context) =>
            new(_title, NavigationTarget.ForPage<ArticleListPage>());

        internal void ChangeTitle(string title)
        {
            _title = title;
            LocationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ExistingPageProvider(object page) : INavigationViewPageProvider
    {
        public object GetPage(Type pageType) => page;
    }

    private sealed class ArticleLocationViewModel(string title) : INotifyPropertyChanged
    {
        private string _title = title;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title
        {
            get => _title;
            set
            {
                if (_title == value)
                {
                    return;
                }

                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    private sealed class ParentLocationViewModel(string parentKey) : INotifyPropertyChanged
    {
        private string _parentKey = parentKey;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ParentKey
        {
            get => _parentKey;
            set
            {
                if (_parentKey == value)
                {
                    return;
                }

                _parentKey = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParentKey)));
            }
        }
    }
}
