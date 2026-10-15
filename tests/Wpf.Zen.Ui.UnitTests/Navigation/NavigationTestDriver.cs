// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests;

/// <summary>
/// Provides an explicit commit barrier for synchronous UI assertions, pumping the Dispatcher while
/// waiting in tests.
/// </summary>
internal static class NavigationTestDriver
{
    internal static NavigationResult NavigateAndCommit(this INavigationService service, Type page, object? parameter = null)
    {
        return Commit(service.Navigate(page, parameter));
    }

    internal static NavigationResult NavigateAndCommit(this INavigationService service, string route, object? parameter = null)
    {
        return Commit(service.Navigate(route, parameter));
    }

    internal static NavigationResult NavigateAndCommit(this INavigationService service, NavigationRequest request)
    {
        return Commit(service.Navigate(request));
    }

    internal static NavigationResult NavigateAndCommit(this INavigationService service, NavigationTarget target)
    {
        return Commit(service.Navigate(target));
    }

    internal static NavigationResult NavigateAndCommit(this INavigationService service, BreadcrumbSegment segment)
    {
        return Commit(service.Navigate(segment));
    }

    internal static NavigationResult NavigateAndCommit(this INavigationHost host, NavigationRequest request)
    {
        return Commit(host.Navigate(request));
    }

    internal static NavigationResult NavigateAndCommit(
        this INavigationRouter router, string host, NavigationRequest request)
    {
        return Commit(router.Navigate(host, request));
    }

    internal static bool NavigateAndCommit(this NavigationView host, Type page)
    {
        var request = NavigationRequest.To(page);
        var result = ((INavigationHost)host).Navigate(request);
        return Commit(result).Succeeded;
    }

    internal static bool NavigateAndCommit(this NavigationView host, string route)
    {
        var request = NavigationRequest.To(route);
        var result = ((INavigationHost)host).Navigate(request);
        return Commit(result).Succeeded;
    }

    internal static NavigationResult GoBackAndCommit(this INavigationService service)
    {
        return Commit(service.GoBack());
    }

    internal static NavigationResult GoForwardAndCommit(this INavigationService service)
    {
        return Commit(service.GoForward());
    }

    internal static bool GoBackAndCommit(this NavigationView host)
    {
        var result = ((INavigationHost)host).RequestGoBack();
        return Commit(result).Succeeded;
    }

    internal static bool GoForwardAndCommit(this NavigationView host)
    {
        var result = ((INavigationHost)host).RequestGoForward();
        return Commit(result).Succeeded;
    }

    /// <summary>
    /// Waits for definitive completion, failing on timeout and preserving original preparation or
    /// projection exception stacks.
    /// </summary>
    internal static NavigationResult Commit(NavigationResult accepted)
    {
        Task<NavigationResult> task = accepted.Completion;
        if (!task.IsCompleted)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Send, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            timeout.Tick += (_, _) => frame.Continue = false;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                TaskScheduler.Default);
            timeout.Start();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timeout.Stop();
            }

            Assert.True(task.IsCompleted, "Navigation did not reach a terminal commit result within 10 seconds.");
        }

        var result = task.GetAwaiter().GetResult();
        if (result.Failure is not null)
        {
            ExceptionDispatchInfo.Capture(result.Failure).Throw();
        }

        return result;
    }
}
