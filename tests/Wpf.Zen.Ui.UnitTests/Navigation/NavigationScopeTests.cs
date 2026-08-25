// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.DependencyInjection;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public class NavigationScopeTests
{
    [Fact]
    public void AddNavigation_ReplacesLegacyGlobalNavigationServiceRegistration()
    {
        WpfTestThread.Run(() =>
        {
            INavigationService legacyService = NSubstitute.Substitute.For<INavigationService>();
            var services = new ServiceCollection();
            _ = services.AddSingleton<INavigationService>(legacyService);
            _ = services.AddNavigation();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var scopedHost = new NavigationView();
            using INavigationScope scope = factory.Attach(scopedHost);

            Assert.NotSame(legacyService, scope.Navigation);
            Assert.Same(scope.Navigation, scope.Services.GetRequiredService<INavigationService>());
        });
    }

    [Fact]
    public void AddNavigation_DoesNotRegisterPageProviderAsAnApplicationService()
    {
        var services = new ServiceCollection();

        _ = services.AddNavigation();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(INavigationViewPageProvider)
        );
    }

    [Fact]
    public void Attach_ActivatesUnregisteredPageAndInjectsScopedDependencies()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddScoped<ScopedDisposable>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            UnregisteredPage? page = null;
            host.Navigated += (_, args) => page = Assert.IsType<UnregisteredPage>(args.Page);

            using INavigationScope scope = factory.Attach(host);

            Assert.True(scope.Navigation.Navigate(typeof(UnregisteredPage)).Succeeded);
            Assert.NotNull(page);
            Assert.Same(
                scope.Services.GetRequiredService<ScopedDisposable>(),
                page.Dependency
            );
            Assert.Null(scope.Services.GetService<INavigationViewPageProvider>());
        });
    }

    [Fact]
    public void Attach_DiScope_RejectsNonFrameworkElementTargetWithSameDiagnostic()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            using INavigationScope scope = factory.Attach(host);

            // The FrameworkElement invariant is enforced at the page-factory chokepoint, so the DI
            // path fails identically to the container-free activator instead of rendering
            // template-less content inside the frame.
            InvalidCastException exception = Assert.Throws<InvalidCastException>(() =>
                scope.Navigation.Navigate(typeof(object)));
            Assert.Contains("must be derived from", exception.Message);
        });
    }

    [Fact]
    public void Attach_RespectsExplicitPageRegistration()
    {
        WpfTestThread.Run(() =>
        {
            var registeredPage = new ProviderCreatedPage("registered-instance");
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddSingleton(registeredPage);

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            ProviderCreatedPage? navigatedPage = null;
            host.Navigated += (_, args) =>
                navigatedPage = Assert.IsType<ProviderCreatedPage>(args.Page);

            using INavigationScope scope = factory.Attach(host);

            Assert.True(scope.Navigation.Navigate(typeof(ProviderCreatedPage)).Succeeded);
            Assert.Same(registeredPage, navigatedPage);
        });
    }

    [Fact]
    public void AttachWithoutContainer_PublishesCompleteNavigationContext()
    {
        WpfTestThread.Run(() =>
        {
            var host = new NavigationView();
            ContainerFreePage? page = null;
            host.Navigated += (_, args) => page = Assert.IsType<ContainerFreePage>(args.Page);

            using INavigationScope scope = Wpf.Zen.Ui.NavigationScope.Attach(host);

            Assert.Same(
                scope.Navigation,
                scope.Services.GetService(typeof(INavigationService))
            );
            Assert.True(
                scope.Navigation.Navigate(typeof(ContainerFreePage), "without-container").Succeeded
            );
            Assert.NotNull(page);
            Assert.Same(scope.Navigation, Wpf.Zen.Ui.Navigation.GetService(page));
            Assert.Same(host, Wpf.Zen.Ui.Navigation.GetHost(page));
            Assert.Equal("without-container", Wpf.Zen.Ui.Navigation.GetParameter(page));

            scope.Dispose();
            Assert.Null(Wpf.Zen.Ui.Navigation.GetService(page));
            Assert.Null(Wpf.Zen.Ui.Navigation.GetHost(page));
            Assert.Null(Wpf.Zen.Ui.Navigation.GetParameter(page));
        });
    }

    [Fact]
    public void AttachWithoutContainer_UsesCallerSuppliedPageProviderAndHostCache()
    {
        WpfTestThread.Run(() =>
        {
            var suppliedPage = new ProviderCreatedPage("created-by-provider");
            var pageProvider = new SinglePageProvider(suppliedPage);
            var host = new NavigationView
            {
                MenuItemsSource = new object[]
                {
                    new NavigationItem("Provided", typeof(ProviderCreatedPage))
                    {
                        NavigationCacheMode = NavigationCacheMode.Permanent,
                    },
                    new NavigationItem("Other", typeof(ContainerFreePage)),
                },
            };
            var providerPageVisits = new List<ProviderCreatedPage>();
            host.Navigated += (_, args) =>
            {
                if (args.Page is ProviderCreatedPage page)
                {
                    providerPageVisits.Add(page);
                }
            };

            using INavigationScope scope = Wpf.Zen.Ui.NavigationScope.Attach(host, pageProvider);
            Assert.Same(
                pageProvider,
                scope.Services.GetService(typeof(INavigationViewPageProvider))
            );
            Assert.True(scope.Navigation.Navigate(typeof(ProviderCreatedPage)).Succeeded);
            Assert.True(scope.Navigation.Navigate(typeof(ContainerFreePage)).Succeeded);
            Assert.True(scope.Navigation.Navigate(typeof(ProviderCreatedPage)).Succeeded);

            Assert.Equal(2, providerPageVisits.Count);
            Assert.Same(suppliedPage, providerPageVisits[0]);
            Assert.Same(providerPageVisits[0], providerPageVisits[1]);
            Assert.Equal(1, pageProvider.ProviderPageRequests);
        });
    }

    [Fact]
    public void Attach_TwoHosts_IsolatesServicesPagesAndParameters()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddTransient<ScopedPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var firstHost = new NavigationView();
            var secondHost = new NavigationView();
            ScopedPage? firstPage = null;
            ScopedPage? secondPage = null;
            firstHost.Navigated += (_, args) => firstPage = Assert.IsType<ScopedPage>(args.Page);
            secondHost.Navigated += (_, args) => secondPage = Assert.IsType<ScopedPage>(args.Page);

            using INavigationScope firstScope = factory.Attach(firstHost, "first");
            using INavigationScope secondScope = factory.Attach(secondHost, "second");

            Assert.NotSame(firstScope.Navigation, secondScope.Navigation);
            Assert.Same(
                firstScope.Navigation,
                firstScope.Services.GetRequiredService<INavigationService>()
            );
            Assert.Same(
                secondScope.Navigation,
                secondScope.Services.GetRequiredService<INavigationService>()
            );

            Assert.True(firstScope.Navigation.Navigate(typeof(ScopedPage), "first-parameter").Succeeded);
            Assert.True(secondScope.Navigation.Navigate(typeof(ScopedPage), "second-parameter").Succeeded);

            Assert.NotNull(firstPage);
            Assert.NotNull(secondPage);
            Assert.NotSame(firstPage, secondPage);
            Assert.Same(firstScope.Navigation, firstPage.NavigationService);
            Assert.Same(secondScope.Navigation, secondPage.NavigationService);
            Assert.Same(firstScope.Navigation, Wpf.Zen.Ui.Navigation.GetService(firstPage));
            Assert.Same(secondScope.Navigation, Wpf.Zen.Ui.Navigation.GetService(secondPage));
            Assert.Equal("first-parameter", Wpf.Zen.Ui.Navigation.GetParameter(firstPage));
            Assert.Equal("second-parameter", Wpf.Zen.Ui.Navigation.GetParameter(secondPage));
            Assert.Same(firstHost, Wpf.Zen.Ui.Navigation.GetHost(firstPage));
            Assert.Same(secondHost, Wpf.Zen.Ui.Navigation.GetHost(secondPage));
        });
    }

    [Fact]
    public void Router_RequiresExplicitLiveHostId()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddTransient<ScopedPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            INavigationRouter router = provider.GetRequiredService<INavigationRouter>();
            var firstHost = new NavigationView();
            var secondHost = new NavigationView();
            var firstNavigated = 0;
            var secondNavigated = 0;
            firstHost.Navigated += (_, _) => firstNavigated++;
            secondHost.Navigated += (_, _) => secondNavigated++;

            using INavigationScope firstScope = factory.Attach(firstHost, "first");
            INavigationScope secondScope = factory.Attach(secondHost, "second");

            Assert.True(router.Navigate("second", NavigationRequest.To<ScopedPage>()).Succeeded);
            Assert.Equal(0, firstNavigated);
            Assert.Equal(1, secondNavigated);

            secondScope.Dispose();

            Assert.Equal(
                NavigationResultStatus.HostNotFound,
                router.Navigate("second", NavigationRequest.To<ScopedPage>()).Status
            );
        });
    }

    [Fact]
    public void DisposingScope_ClearsContextAndAllowsHostToBeAttachedAgain()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddTransient<ScopedPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            ScopedPage? page = null;
            host.Navigated += (_, args) => page = Assert.IsType<ScopedPage>(args.Page);
            INavigationScope firstScope = factory.Attach(host);

            Assert.True(firstScope.Navigation.Navigate(typeof(ScopedPage)).Succeeded);
            Assert.NotNull(page);
            firstScope.Dispose();

            Assert.Null(Wpf.Zen.Ui.Navigation.GetService(page));
            Assert.Null(Wpf.Zen.Ui.Navigation.GetHost(page));

            using INavigationScope secondScope = factory.Attach(host);
            Assert.NotSame(firstScope.Navigation, secondScope.Navigation);
            Assert.True(secondScope.Navigation.Navigate(typeof(ScopedPage)).Succeeded);
            Assert.NotNull(page);
            Assert.Same(secondScope.Navigation, page.NavigationService);
            Assert.Same(secondScope.Navigation, Wpf.Zen.Ui.Navigation.GetService(page));
        });
    }

    [Fact]
    public void ScopedPageProvider_UsesNavigationViewCachePolicy()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddTransient<ScopedPage>();
            _ = services.AddTransient<OtherScopedPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var cachedItem = new NavigationItem("Cached", typeof(ScopedPage))
            {
                NavigationCacheMode = NavigationCacheMode.Permanent,
            };
            var host = new NavigationView
            {
                MenuItemsSource = new object[]
                {
                    cachedItem,
                    new NavigationItem("Other", typeof(OtherScopedPage)),
                },
            };
            var scopedPages = new List<ScopedPage>();
            host.Navigated += (_, args) =>
            {
                if (args.Page is ScopedPage scopedPage)
                {
                    scopedPages.Add(scopedPage);
                }
            };

            using INavigationScope scope = factory.Attach(host);
            Assert.True(scope.Navigation.Navigate(typeof(ScopedPage)).Succeeded);
            Assert.True(scope.Navigation.Navigate(typeof(OtherScopedPage)).Succeeded);
            Assert.True(scope.Navigation.Navigate(typeof(ScopedPage)).Succeeded);

            Assert.Equal(2, scopedPages.Count);
            Assert.Same(scopedPages[0], scopedPages[1]);
        });
    }

    [Fact]
    public void Dispose_CancelsEntryAndExitsBeforeDisposingScopedDependencies()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddScoped<ScopedDisposable>();
            _ = services.AddTransient<CancellableEntryPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            CancellableEntryPage? page = null;
            host.Navigated += (_, args) => page = Assert.IsType<CancellableEntryPage>(args.Page);
            using INavigationScope scope = factory.Attach(host);

            Assert.True(scope.Navigation.Navigate(typeof(CancellableEntryPage)).Succeeded);
            Dispatcher.CurrentDispatcher.Invoke(
                static () => { },
                DispatcherPriority.ApplicationIdle
            );
            Assert.NotNull(page);
            Assert.False(page.EntryCancellationToken.IsCancellationRequested);
            Assert.False(page.Dependency.IsDisposed);

            scope.Dispose();

            Assert.True(page.EntryCancellationToken.IsCancellationRequested);
            Assert.True(page.ExitedBeforeDependencyDisposal);

            // Dispose initiates foreign-thread-safe cleanup without blocking the WPF dispatcher.
            // Await the explicit completion path before asserting service-scope disposal.
            scope.DisposeAsync().GetAwaiter().GetResult();
            Assert.True(page.Dependency.IsDisposed);
        });
    }

    [Fact]
    public void GoBack_RestoresParameterFromTheJournalOccurrence()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddTransient<ScopedPage>();
            _ = services.AddTransient<OtherScopedPage>();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView
            {
                MenuItemsSource = new object[]
                {
                    new NavigationItem("First", typeof(ScopedPage))
                    {
                        NavigationCacheMode = NavigationCacheMode.Permanent,
                    },
                    new NavigationItem("Second", typeof(OtherScopedPage)),
                },
            };
            ScopedPage? firstPage = null;
            host.Navigated += (_, args) => firstPage = args.Page as ScopedPage ?? firstPage;
            using INavigationScope scope = factory.Attach(host);

            Assert.True(scope.Navigation.Navigate(typeof(ScopedPage), "first-visit").Succeeded);
            Assert.True(scope.Navigation.Navigate(typeof(OtherScopedPage), "second-visit").Succeeded);
            Assert.True(scope.Navigation.GoBack().Succeeded);

            Assert.NotNull(firstPage);
            Assert.Equal("first-visit", Wpf.Zen.Ui.Navigation.GetParameter(firstPage));
        });
    }

    [Fact]
    public void Attach_SameHostTwice_RejectsSecondScopeWithoutReplacingFirst()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var host = new NavigationView();
            using INavigationScope firstScope = factory.Attach(host);

            _ = Assert.Throws<InvalidOperationException>(() =>
            {
                using INavigationScope unexpectedScope = factory.Attach(host);
            });
            Assert.Same(
                firstScope.Navigation,
                firstScope.Services.GetRequiredService<INavigationService>()
            );
        });
    }

    [Fact]
    public void Attach_DuplicateRouteId_DoesNotAttachOrPoisonSecondHost()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();

            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var firstHost = new NavigationView();
            var secondHost = new NavigationView();
            using INavigationScope firstScope = factory.Attach(firstHost, "duplicate");

            _ = Assert.Throws<InvalidOperationException>(() =>
            {
                using INavigationScope unexpectedScope = factory.Attach(secondHost, "duplicate");
            });

            using INavigationScope secondScope = factory.Attach(secondHost, "second");
            Assert.NotSame(firstScope.Navigation, secondScope.Navigation);
        });
    }

    private sealed class ScopedPage(INavigationService navigationService) : Border
    {
        public INavigationService NavigationService { get; } = navigationService;
    }

    private sealed class OtherScopedPage : Border { }

    private sealed class ContainerFreePage : Border { }

    private sealed class UnregisteredPage(ScopedDisposable dependency) : Border
    {
        public ScopedDisposable Dependency { get; } = dependency;
    }

    private sealed class ProviderCreatedPage(string source) : Border
    {
        public string Source { get; } = source;
    }

    private sealed class SinglePageProvider(ProviderCreatedPage page) : INavigationViewPageProvider
    {
        public int ProviderPageRequests { get; private set; }

        public object GetPage(Type pageType)
        {
            if (pageType == typeof(ProviderCreatedPage))
            {
                ProviderPageRequests++;
                return page;
            }

            return ActivatorNavigationViewPageProvider.Instance.GetPage(pageType);
        }
    }

    private sealed class CancellableEntryPage(ScopedDisposable dependency)
        : Border, IAsyncNavigationAware
    {
        public ScopedDisposable Dependency { get; } = dependency;

        public CancellationToken EntryCancellationToken { get; private set; }

        public bool ExitedBeforeDependencyDisposal { get; private set; }

        public Task OnNavigationEnteredAsync(CancellationToken cancellationToken)
        {
            EntryCancellationToken = cancellationToken;
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public void OnNavigationExited()
        {
            ExitedBeforeDependencyDisposal = !Dependency.IsDisposed;
        }
    }

    private sealed class ScopedDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
