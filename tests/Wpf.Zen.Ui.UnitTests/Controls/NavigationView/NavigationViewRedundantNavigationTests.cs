// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using NavigationControl = Wpf.Zen.Ui.Controls.NavigationView;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

/// <summary>
/// Verifies that menu shortcuts and service requests share destination identity.
/// </summary>
public class NavigationViewRedundantNavigationTests
{
    /// <summary>
    /// Clicking the selected shortcut preserves content, lifecycle, location, and forward history.
    /// </summary>
    [Theory]
    [InlineData("type", false, false)]
    [InlineData("type", true, true)]
    [InlineData("id", false, true)]
    [InlineData("id", true, false)]
    [InlineData("tag", false, false)]
    [InlineData("tag", true, true)]
    [InlineData("route", false, true)]
    [InlineData("route", true, false)]
    public void ServiceNavigation_ClickSelectedItem_DoesNotNavigateAgain(
        string lookup, bool useDataItem, bool cachePage)
    {
        WpfTestThread.Run(async () =>
        {
            var cacheMode = cachePage ? NavigationCacheMode.Permanent : NavigationCacheMode.Disabled;
            var navigation = new NavigationControl();
            var itemId = "destination-id";
            object item;
            if (useDataItem)
            {
                item = new NavigationItem("Destination", typeof(DestinationPage))
                {
                    Id = "destination-id",
                    TargetPageTag = "destination-tag",
                    NavigationCacheMode = cacheMode,
                };
            }
            else
            {
                var directItem = new NavigationViewItem("Destination", typeof(DestinationPage))
                {
                    TargetPageTag = "destination-tag",
                    NavigationCacheMode = cacheMode,
                };
                item = directItem;
                itemId = directItem.Id;
            }

            navigation.MenuItems.Add(item);
            if (lookup == "route")
            {
                navigation.NavigationMap.Add<DestinationPage>("destination-route", "Destination");
            }

            await using var scope = NavigationScope.Attach(navigation);
            var window = new Window
            {
                Content = navigation,
                Width = 800,
                Height = 450,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                DrainDispatcher();
                var target = lookup switch
                {
                    "id" => NavigationTarget.ForRoute(itemId),
                    "tag" => NavigationTarget.ForRoute("destination-tag"),
                    "route" => NavigationTarget.ForRoute("destination-route"),
                    _ => NavigationTarget.ForPage<DestinationPage>(),
                };
                Assert.True(scope.Navigation.NavigateAndCommit(target).Succeeded);
                Assert.True(scope.Navigation.NavigateAndCommit(typeof(Border)).Succeeded);
                Assert.True(scope.Navigation.GoBackAndCommit().Succeeded);
                DrainDispatcher();

                var location = navigation.CurrentLocation;
                var page = Assert.IsType<DestinationPage>(location?.Page);
                var model = Assert.IsType<LifecycleModel>(page.DataContext);
                var entered = model.Entered;
                var exited = model.Exited;
                Assert.True(entered > 0);
                Assert.Same(item, navigation.SelectedItem);
                Assert.True(navigation.CanGoForward);
                Assert.False(navigation.CanGoBack);

                var list = Assert.IsType<NavigationViewList>(
                    navigation.Template.FindName("PART_MenuItemsList", navigation));
                var container = Assert.IsType<NavigationViewItem>(
                    list.ItemContainerGenerator.ContainerFromItem(item));
                Assert.True(container.IsActive);
                var navigatingCount = 0;
                var invokedCount = 0;
                navigation.Navigating += (_, _) => navigatingCount++;
                navigation.ItemInvoked += (_, _) => invokedCount++;

                for (var click = 0; click < 2; click++)
                {
                    navigation.OnNavigationViewItemClick(container);
                    DrainDispatcher();
                    Assert.Same(location, navigation.CurrentLocation);
                    Assert.Equal(entered, model.Entered);
                    Assert.Equal(exited, model.Exited);
                    Assert.True(navigation.CanGoForward);
                    Assert.False(navigation.CanGoBack);
                }

                Assert.Equal(0, navigatingCount);
                Assert.Equal(2, invokedCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Type, item id, and item tag are equivalent lookup forms in either direction.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquivalentServiceLookups_AreRejectedWithoutChangingLocation(bool mapPage)
    {
        WpfTestThread.Run(async () =>
        {
            var navigation = new NavigationControl();
            navigation.MenuItems.Add(new NavigationItem("Destination", typeof(DestinationPage))
            {
                Id = "id",
                TargetPageTag = "tag",
            });
            if (mapPage)
            {
                navigation.NavigationMap.Add<DestinationPage>("route", "Destination");
            }

            await using var scope = NavigationScope.Attach(navigation);
            var targets = new[]
            {
                NavigationTarget.ForPage<DestinationPage>(),
                NavigationTarget.ForRoute("id"),
                NavigationTarget.ForRoute("tag"),
            };
            foreach (var initial in targets)
            {
                Assert.True(scope.Navigation.NavigateAndCommit(typeof(Border)).Succeeded);
                Assert.True(scope.Navigation.NavigateAndCommit(initial).Succeeded);
                var location = navigation.CurrentLocation;
                foreach (var equivalent in targets)
                {
                    var result = scope.Navigation.NavigateAndCommit(equivalent);
                    Assert.Equal(NavigationResultStatus.Rejected, result.Status);
                    Assert.Same(location, navigation.CurrentLocation);
                }
            }
        });
    }

    /// <summary>
    /// Semantic routes, occurrence parameters, and explicit path metadata retain their meaning.
    /// </summary>
    [Fact]
    public void SameMenuEntry_DistinctOccurrences_StillNavigate()
    {
        WpfTestThread.Run(async () =>
        {
            var navigation = new NavigationControl();
            navigation.MenuItems.Add(new NavigationItem("Destination", typeof(DestinationPage))
            {
                Id = "id",
                TargetPageTag = "tag",
            });
            // Even a route whose key equals an item lookup key has semantic identity in the map.
            navigation.NavigationMap.Add<DestinationPage>("id", "First placement");
            navigation.NavigationMap.Add<DestinationPage>("tag", "Second placement");
            await using var scope = NavigationScope.Attach(navigation);

            Assert.True(scope.Navigation.NavigateAndCommit("id").Succeeded);
            Assert.True(scope.Navigation.NavigateAndCommit("tag").Succeeded);
            Assert.True(scope.Navigation.NavigateAndCommit("tag", "parameter").Succeeded);
            var request = NavigationRequest.To("tag", "parameter").WithBreadcrumbContent("Custom");
            Assert.True(scope.Navigation.NavigateAndCommit(request).Succeeded);
            Assert.Equal("Custom", Assert.Single(navigation.Breadcrumbs).Content);
            Assert.True(navigation.CanGoBack);
        });
    }

    /// <summary>
    /// Runs pending frame commits and lifecycle dispatch before inspecting state.
    /// </summary>
    private static void DrainDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Supplies a fresh view model so both cached and newly created pages expose lifecycle changes.
    /// </summary>
    private sealed class DestinationPage : Border
    {
        /// <summary>
        /// Initializes the page's lifecycle participant.
        /// </summary>
        public DestinationPage()
        {
            DataContext = new LifecycleModel();
        }
    }

    /// <summary>
    /// Counts entry and exit notifications independently of page instance identity.
    /// </summary>
    private sealed class LifecycleModel : INavigationAware
    {
        /// <summary>
        /// Gets the number of completed lifecycle entries.
        /// </summary>
        public int Entered { get; private set; }

        /// <summary>
        /// Gets the number of lifecycle exits.
        /// </summary>
        public int Exited { get; private set; }

        /// <inheritdoc />
        public void OnNavigationEntered()
        {
            Entered++;
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            Exited++;
        }
    }
}