// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines the WPF <see cref="Frame"/> used as the stable navigation surface of
/// <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// Stores the control that currently publishes <see cref="ContentPaddingProperty"/> on a
    /// navigation root.
    /// </summary>
    /// <remarks>
    /// This private marker is required because a page style supplied through DynamicResource can
    /// set <see cref="IsContentPaddingHandledProperty"/> after <see cref="Frame.Navigated"/>. The
    /// property-changed callback can then update the correct frame without a visual-tree search.
    /// </remarks>
    private static readonly DependencyProperty ContentPaddingHostProperty =
        DependencyProperty.RegisterAttached(
            "ContentPaddingHost",
            typeof(NavigationView),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// The tail of the navigation-aware notification queue for this control instance.
    /// </summary>
    private Task _navigationNotificationTask = Task.CompletedTask;

    /// <summary>
    /// The content from the latest accepted navigation request.
    /// </summary>
    /// <remarks>
    /// WPF can defer updating <see cref="ContentControl.Content"/> until after
    /// <see cref="Frame.Navigate(object)"/> returns. Tracking the requested content separately
    /// ensures that two requests issued in the same dispatcher turn still identify the second page
    /// as the one departing during the third request.
    /// </remarks>
    private object? _navigationAwareContent;

    /// <summary>
    /// The navigation root on which this control has published
    /// <see cref="ContentPaddingProperty"/>.
    /// </summary>
    /// <remarks>
    /// Keeping the owner explicit lets navigation remove control-owned attached state from a
    /// cached page before that page is handed to another host.
    /// </remarks>
    private DependencyObject? _contentPaddingOwner;

    /// <summary>
    /// Creates the frame that owns WPF navigation state for this control.
    /// </summary>
    /// <remarks>
    /// The frame is deliberately composed rather than subclassed. No additional public control is
    /// required: <see cref="NavigationView"/> owns the navigation policy, while WPF's
    /// <see cref="Frame"/> supplies the navigation service and its required
    /// <c>PART_FrameCP</c> presentation contract.
    ///
    /// The frame owns a private WPF journal so its entries can never leak into an ancestor
    /// <see cref="NavigationWindow"/>. <see cref="NavigationView"/> maintains its own semantic
    /// journal, so frame entries are removed after navigation and the frame's routed navigation
    /// commands are removed below.
    /// </remarks>
    private static Frame CreateNavigationFrame()
    {
        var frame = new Frame
        {
            Focusable = false,
            FocusVisualStyle = null,
            IsTabStop = false,
            JournalOwnership = JournalOwnership.OwnsJournal,
            NavigationUIVisibility = NavigationUIVisibility.Hidden,
            SnapsToDevicePixels = true,
        };

        RemoveFrameNavigationCommandBindings(frame);

        return frame;
    }

    /// <summary>
    /// Removes command bindings that WPF installs when a <see cref="Frame"/> owns its journal.
    /// </summary>
    /// <remarks>
    /// WPF's <c>Frame.SwitchToOwnJournal</c> installs handlers for back, forward, journal,
    /// refresh, and stop commands. Leaving those handlers in place would make F5 refresh the
    /// current page behind <see cref="NavigationView"/>'s journal and lifecycle model, and their
    /// <c>CanExecute</c> handlers can consume commands even when the private frame journal is
    /// empty. Removing the bindings disables only frame-owned navigation; it does not swallow raw
    /// keyboard or mouse input, so a page or an ancestor remains free to handle those gestures.
    /// </remarks>
    private static void RemoveFrameNavigationCommandBindings(Frame frame)
    {
        for (int index = frame.CommandBindings.Count - 1; index >= 0; index--)
        {
            if (
                frame.CommandBindings[index].Command == NavigationCommands.BrowseBack
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseForward
                || frame.CommandBindings[index].Command == NavigationCommands.NavigateJournal
                || frame.CommandBindings[index].Command == NavigationCommands.Refresh
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseStop
            )
            {
                frame.CommandBindings.Remove(frame.CommandBindings[index]);
            }
        }
    }

    /// <summary>
    /// Dispatches navigation-aware leave and enter notifications in a deterministic order.
    /// </summary>
    /// <remarks>
    /// WPF navigation events are synchronous and do not provide an awaitable cancellation model.
    /// Consequently, only the public <see cref="Navigating"/> event can cancel navigation; the
    /// asynchronous awareness callbacks are notifications. Leave completes before enter so a
    /// shared view model cannot have a newly started operation cancelled by the previous page's
    /// cleanup.
    /// </remarks>
    private async void OnNavigationFrameNavigating(
        object sender,
        System.Windows.Navigation.NavigatingCancelEventArgs eventArgs
    )
    {
        if (eventArgs.Content is null)
        {
            return;
        }

        object? departingContent = _navigationAwareContent;
        object arrivingContent = eventArgs.Content;
        _navigationAwareContent = arrivingContent;

        await QueueNavigationNotificationAsync(async () =>
        {
            await NotifyContentAboutNavigatingFromAsync(departingContent);
            await NotifyContentAboutNavigatingToAsync(arrivingContent);
        });
    }

    /// <summary>
    /// Applies the visual transition and discards WPF's implementation journal entry.
    /// </summary>
    /// <remarks>
    /// Selection and hierarchy semantics live in <see cref="NavigationView"/>'s own journal.
    /// Retaining the frame entry would create a second, independently navigable history.
    /// </remarks>
    private void OnNavigationFrameNavigated(object sender, NavigationEventArgs eventArgs)
    {
        _ = _navigationFrame.RemoveBackEntry();

        SetNavigatedContentPaddingOwner(eventArgs.Content as DependencyObject);

        if (eventArgs.Content is not null)
        {
            ApplyTransitionEffectToNavigationFrame();
        }
    }

    /// <summary>
    /// Replaces the root on which this control publishes <see cref="ContentPaddingProperty"/>.
    /// </summary>
    /// <param name="newOwner">
    /// The newly navigated root, or <see langword="null"/> when the frame no longer has content.
    /// </param>
    /// <remarks>
    /// Frame sets <see cref="InheritanceBehavior"/> to
    /// <see cref="InheritanceBehavior.SkipToAppNow"/> in the WPF constructor. That isolation is
    /// intentional for navigation, but it also prevents page-owned scroll viewers from inheriting
    /// NavigationView's content inset. A read-only attached value provides the narrow bridge needed
    /// for layout while leaving the page's own Margin, Padding, and scrolling properties untouched.
    /// </remarks>
    private void SetNavigatedContentPaddingOwner(DependencyObject? newOwner)
    {
        if (!ReferenceEquals(_contentPaddingOwner, newOwner))
        {
            _contentPaddingOwner?.ClearValue(ContentPaddingPropertyKey);
            _contentPaddingOwner?.ClearValue(ContentPaddingHostProperty);
            _contentPaddingOwner = newOwner;
            _contentPaddingOwner?.SetCurrentValue(ContentPaddingHostProperty, this);
        }

        UpdateNavigatedContentPadding();
    }

    /// <summary>
    /// Refreshes the host-provided inset after <see cref="Control.Padding"/> changes.
    /// </summary>
    private void UpdateNavigatedContentPadding()
    {
        _contentPaddingOwner?.SetValue(ContentPaddingPropertyKey, Padding);
        UpdateNavigationFramePadding();
    }

    /// <summary>
    /// Applies the content inset at exactly one side of the page boundary.
    /// </summary>
    /// <remarks>
    /// Ordinary content receives Padding from WPF's native Frame template. Content that declares
    /// <see cref="IsContentPaddingHandledProperty"/> keeps Frame.Padding empty and consumes the
    /// separately published <see cref="ContentPaddingProperty"/> inside its own viewport. This
    /// preserves ordinary-page layout while keeping page-owned scroll bars at the outer edge.
    /// </remarks>
    private void UpdateNavigationFramePadding()
    {
        bool isHandledByContent =
            _contentPaddingOwner is not null && GetIsContentPaddingHandled(_contentPaddingOwner);

        _navigationFrame.SetCurrentValue(
            Control.PaddingProperty,
            isHandledByContent ? default(Thickness) : Padding
        );
    }

    /// <summary>
    /// Notifies the current page when the complete navigation view leaves a loaded visual tree.
    /// </summary>
    /// <remarks>
    /// Changing <see cref="PaneDisplayMode"/> moves the same frame between template hosts and
    /// transiently raises <see cref="FrameworkElement.Unloaded"/>. Deferring until context-idle
    /// distinguishes that move from a genuine window, tab, or application detach without coupling
    /// the frame to any one built-in template.
    /// </remarks>
    private async void OnNavigationFrameUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        await System.Windows.Threading.Dispatcher.Yield(
            System.Windows.Threading.DispatcherPriority.ContextIdle
        );

        if (_navigationFrame.IsLoaded)
        {
            return;
        }

        object? content = _navigationAwareContent;
        _navigationAwareContent = null;
        await QueueNavigationNotificationAsync(() => NotifyContentAboutNavigatingFromAsync(content));
    }

    /// <summary>
    /// Serializes asynchronous page notifications across rapid navigation and unload operations.
    /// </summary>
    /// <remarks>
    /// A WPF <see cref="Frame"/> does not await event handlers. Without this queue, a second
    /// navigation can notify its page before the first page has finished leaving. A failure from a
    /// previous notification is ignored only by the queue continuation—the originating async event
    /// handler still observes and reports that failure—so one faulty page cannot permanently stop
    /// later lifecycle notifications.
    /// </remarks>
    private Task QueueNavigationNotificationAsync(Func<Task> notification)
    {
        Task precedingTask = _navigationNotificationTask;
        var completion = new TaskCompletionSource<object?>();

        // Publish the queue entry before starting it. A navigation-aware callback is allowed to
        // request another navigation synchronously; that reentrant request must see this entry as
        // its predecessor rather than bypassing the callback that is currently running.
        _navigationNotificationTask = completion.Task;
        _ = CompleteQueuedNotificationAsync(precedingTask, notification, completion);

        return completion.Task;
    }

    private async Task CompleteQueuedNotificationAsync(
        Task precedingTask,
        Func<Task> notification,
        TaskCompletionSource<object?> completion
    )
    {
        try
        {
            await RunNotificationAfterAsync(precedingTask, notification);
            completion.SetResult(null);
        }
        catch (Exception exception)
        {
            completion.SetException(exception);
        }
    }

    private Task RunNotificationAfterAsync(Task precedingTask, Func<Task> notification) =>
        precedingTask
            .ContinueWith(
                _ => InvokeNotificationOnDispatcherAsync(notification),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            )
            .Unwrap();

    /// <summary>
    /// Starts a queued notification on the frame's dispatcher.
    /// </summary>
    /// <remarks>
    /// <see cref="TaskContinuationOptions.ExecuteSynchronously"/> keeps a synchronously completed
    /// chain in the current dispatcher turn. When a page completes on a worker thread, this method
    /// explicitly marshals the next callback before it can read dispatcher-affine page state.
    /// </remarks>
    private Task InvokeNotificationOnDispatcherAsync(Func<Task> notification)
    {
        if (_navigationFrame.Dispatcher.CheckAccess())
        {
            return notification();
        }

        return _navigationFrame.Dispatcher.InvokeAsync(notification).Task.Unwrap();
    }

    /// <summary>
    /// Applies the configured transition to the control-owned frame.
    /// </summary>
    /// <remarks>
    /// Animating the page itself would replace or hold animation clocks on consumer-owned
    /// <see cref="UIElement.Opacity"/> and <see cref="UIElement.RenderTransform"/> properties. The
    /// stable frame is an implementation detail, so animating it produces the same visual result
    /// without mutating the navigated page.
    /// </remarks>
    private void ApplyTransitionEffectToNavigationFrame()
    {
        if (TransitionDuration < 1)
        {
            return;
        }

        _ = TransitionAnimationProvider.ApplyTransition(_navigationFrame, Transition, TransitionDuration);
    }

    private static Task NotifyContentAboutNavigatingToAsync(object? content) =>
        NotifyContentAboutNavigatingAsync(content, navigationAware => navigationAware.OnNavigatedToAsync());

    private static Task NotifyContentAboutNavigatingFromAsync(object? content) =>
        NotifyContentAboutNavigatingAsync(content, navigationAware => navigationAware.OnNavigatedFromAsync());

    /// <summary>
    /// Invokes a navigation notification for both a view and its distinct view model.
    /// </summary>
    /// <remarks>
    /// View and view-model notifications are isolated so an exception from the view cannot prevent
    /// the view model from performing required cleanup. The original view exception is rethrown
    /// after the view-model callback completes.
    /// </remarks>
    private static async Task NotifyContentAboutNavigatingAsync(
        object? content,
        Func<INavigationAware, Task> notification
    )
    {
        switch (content)
        {
            case INavigationAware aware:
            {
                // Capture the view model before the first await. Navigation-aware implementations
                // are allowed to complete on any scheduler, but DependencyObject properties remain
                // dispatcher-affine.
                INavigationAware? viewModel =
                    aware is FrameworkElement frameworkElement
                    && !ReferenceEquals(frameworkElement.DataContext, aware)
                        ? frameworkElement.DataContext as INavigationAware
                        : null;
                ExceptionDispatchInfo? viewException = null;

                try
                {
                    await notification(aware);
                }
                catch (Exception exception)
                {
                    viewException = ExceptionDispatchInfo.Capture(exception);
                }

                if (viewModel is not null)
                {
                    await notification(viewModel);
                }

                viewException?.Throw();
                break;
            }

            case INavigableView<object> { ViewModel: INavigationAware viewModel }:
            {
                await notification(viewModel);
                break;
            }

            case FrameworkElement { DataContext: INavigationAware dataContext }:
            {
                await notification(dataContext);
                break;
            }
        }
    }
}
