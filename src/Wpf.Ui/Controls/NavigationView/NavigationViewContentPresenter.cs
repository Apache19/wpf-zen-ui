// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library */

using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class NavigationViewContentPresenter : Frame
{
    /// <summary>Identifies the <see cref="TransitionDuration"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration),
        typeof(int),
        typeof(NavigationViewContentPresenter),
        new FrameworkPropertyMetadata(200)
    );

    /// <summary>Identifies the <see cref="Transition"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition),
        typeof(Transition),
        typeof(NavigationViewContentPresenter),
        new FrameworkPropertyMetadata(Transition.FadeInWithSlide)
    );

    /// <summary>Identifies the <see cref="IsDynamicScrollViewerEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsDynamicScrollViewerEnabledProperty =
        DependencyProperty.Register(
            nameof(IsDynamicScrollViewerEnabled),
            typeof(bool),
            typeof(NavigationViewContentPresenter),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure)
        );

    [Bindable(true)]
    [Category("Appearance")]
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>
    /// Gets or sets type of <see cref="NavigationViewContentPresenter"/> transitions during navigation.
    /// </summary>
    public Transition Transition
    {
        get => (Transition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the dynamic scroll viewer is enabled.
    /// </summary>
    public bool IsDynamicScrollViewerEnabled
    {
        get => (bool)GetValue(IsDynamicScrollViewerEnabledProperty);
        protected set => SetValue(IsDynamicScrollViewerEnabledProperty, value);
    }

    static NavigationViewContentPresenter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewContentPresenter),
            new FrameworkPropertyMetadata(typeof(NavigationViewContentPresenter))
        );

        NavigationUIVisibilityProperty.OverrideMetadata(
            typeof(NavigationViewContentPresenter),
            new FrameworkPropertyMetadata(NavigationUIVisibility.Hidden)
        );

        SandboxExternalContentProperty.OverrideMetadata(
            typeof(NavigationViewContentPresenter),
            new FrameworkPropertyMetadata(true)
        );

        JournalOwnershipProperty.OverrideMetadata(
            typeof(NavigationViewContentPresenter),
            new FrameworkPropertyMetadata(JournalOwnership.UsesParentJournal)
        );

        if (
            ScrollViewer.CanContentScrollProperty.GetMetadata(typeof(Page))
         == ScrollViewer.CanContentScrollProperty.DefaultMetadata
        )
        {
            ScrollViewer.CanContentScrollProperty.OverrideMetadata(
                typeof(Page),
                new FrameworkPropertyMetadata(true)
            );
        }
    }

    public NavigationViewContentPresenter()
    {
        Navigating += static async (sender, eventArgs) =>
        {
            if (eventArgs.Content is null)
            {
                return;
            }

            var self = (NavigationViewContentPresenter)sender;
            await self.OnNavigatingCoreAsync(eventArgs);
        };

        Navigated += static (sender, eventArgs) =>
        {
            var self = (NavigationViewContentPresenter)sender;

            if (eventArgs.Content is null)
            {
                return;
            }

            self.OnNavigated(eventArgs);
        };
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // Unloaded handles cleanup when Frame is removed from visual tree
        // (window close, tab switch, app exit), since Navigating won't fire.
        // Normal navigation already triggers FROM via OnNavigatingCoreAsync.
        Unloaded += static async (sender, _) =>
        {
            if (sender is NavigationViewContentPresenter navigator)
            {
                await NotifyContentAboutNavigatingFromAsync(navigator.Content);
            }
        };
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2)
        {
            e.Handled = true;
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private async Task OnNavigatingCoreAsync(System.Windows.Navigation.NavigatingCancelEventArgs eventArgs)
    {
        OnNavigating(eventArgs);

        // First notify current page to leave (OnNavigatedFromAsync),
        // then notify new page to enter (OnNavigatedToAsync).
        // Reversed order (TO first, then FROM) causes issues when old and new
        // pages share the same INavigationAware instance (e.g., ViewModel as singleton),
        // because OnNavigatedFromAsync would cancel the async operation
        // just started by OnNavigatedToAsync.
        if (eventArgs.Navigator is NavigationViewContentPresenter navigator)
        {
            await NotifyContentAboutNavigatingFromAsync(navigator.Content);
        }

        await NotifyContentAboutNavigatingToAsync(eventArgs.Content);
    }

    protected virtual void OnNavigating(System.Windows.Navigation.NavigatingCancelEventArgs eventArgs)
    {
    }

    protected virtual void OnNavigated(NavigationEventArgs eventArgs)
    {
        ApplyTransitionEffectToNavigatedPage(eventArgs.Content);

        if (eventArgs.Content is not DependencyObject dependencyObject)
        {
            return;
        }

        SetCurrentValue(
            IsDynamicScrollViewerEnabledProperty,
            ScrollViewer.GetCanContentScroll(dependencyObject)
        );
    }

    private void ApplyTransitionEffectToNavigatedPage(object content)
    {
        if (TransitionDuration < 1)
        {
            return;
        }

        _ = TransitionAnimationProvider.ApplyTransition(content, Transition, TransitionDuration);
    }

    private static async Task NotifyContentAboutNavigatingToAsync(object content)
    {
        await NotifyContentAboutNavigatingAsync(
            content,
            navigationAware => navigationAware.OnNavigatedToAsync()
        );
    }

    private static async Task NotifyContentAboutNavigatingFromAsync(object content)
    {
        await NotifyContentAboutNavigatingAsync(
            content,
            navigationAware => navigationAware.OnNavigatedFromAsync()
        );
    }

    private static async Task NotifyContentAboutNavigatingAsync(object content, Func<INavigationAware, Task> function)
    {
        switch (content)
        {
            case INavigationAware aware:
            {
                // The notifications between View and ViewModel are isolated:
                // an exception thrown by the View should not prevent the ViewModel from receiving the notification.
                ExceptionDispatchInfo? viewException = null;

                try
                {
                    await function(aware);
                }
                catch (Exception ex)
                {
                    viewException = ExceptionDispatchInfo.Capture(ex);
                }

                if (aware is FrameworkElement { DataContext: INavigationAware vm } &&
                    !ReferenceEquals(vm, aware))
                {
                    await function(vm);
                }

                viewException?.Throw();

                break;
            }

            case INavigableView<object> { ViewModel: INavigationAware vm }:
            {
                await function(vm);
                break;
            }

            case FrameworkElement { DataContext: INavigationAware dc }:
            {
                await function(dc);
                break;
            }
        }
    }
}