// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines events for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    #region PaneOpened

    /// <summary>Identifies the <see cref="PaneOpened"/> routed event.</summary>
    public static readonly RoutedEvent PaneOpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(PaneOpened),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when the navigation pane is opened.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> PaneOpened
    {
        add => AddHandler(PaneOpenedEvent, value);
        remove => RemoveHandler(PaneOpenedEvent, value);
    }

    #endregion

    #region PaneClosed

    /// <summary>Identifies the <see cref="PaneClosed"/> routed event.</summary>
    public static readonly RoutedEvent PaneClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(PaneClosed),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when the navigation pane is closed.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> PaneClosed
    {
        add => AddHandler(PaneClosedEvent, value);
        remove => RemoveHandler(PaneClosedEvent, value);
    }

    #endregion

    #region PaneDisplayModeChanged

    /// <summary>Identifies the <see cref="PaneDisplayModeChanged"/> routed event.</summary>
    public static readonly RoutedEvent PaneDisplayModeChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(PaneDisplayModeChanged),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when <see cref="PaneDisplayMode"/> changes.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> PaneDisplayModeChanged
    {
        add => AddHandler(PaneDisplayModeChangedEvent, value);
        remove => RemoveHandler(PaneDisplayModeChangedEvent, value);
    }

    #endregion

    #region SelectionChanged

    /// <summary>Identifies the <see cref="SelectionChanged"/> routed event.</summary>
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionChanged),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when the currently selected navigation item changes.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    #region ItemInvoked

    /// <summary>Identifies the <see cref="ItemInvoked"/> routed event.</summary>
    public static readonly RoutedEvent ItemInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(ItemInvoked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when a menu item receives an interaction such as a click or tap.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    #endregion

    #region BackRequested

    /// <summary>Identifies the <see cref="BackRequested"/> routed event.</summary>
    public static readonly RoutedEvent BackRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(BackRequested),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, RoutedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs when the back button receives an interaction such as a click or tap.</summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs> BackRequested
    {
        add => AddHandler(BackRequestedEvent, value);
        remove => RemoveHandler(BackRequestedEvent, value);
    }

    #endregion

    #region Navigating

    /// <summary>Identifies the <see cref="Navigating"/> routed event.</summary>
    public static readonly RoutedEvent NavigatingEvent = EventManager.RegisterRoutedEvent(
        nameof(Navigating),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, NavigatingEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs before a requested navigation is performed and allows it to be cancelled.</summary>
    public event TypedEventHandler<NavigationView, NavigatingEventArgs> Navigating
    {
        add => AddHandler(NavigatingEvent, value);
        remove => RemoveHandler(NavigatingEvent, value);
    }

    #endregion

    #region Navigated

    /// <summary>Identifies the <see cref="Navigated"/> routed event.</summary>
    public static readonly RoutedEvent NavigatedEvent = EventManager.RegisterRoutedEvent(
        nameof(Navigated),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationView, NavigatedEventArgs>),
        typeof(NavigationView)
    );

    /// <summary>Occurs after the host commits a page to its semantic navigation state.</summary>
    /// <remarks>
    /// This event is synchronous with the accepted request so selection, history, and
    /// <see cref="CurrentLocation"/> are already observable by its handlers. WPF may attach the
    /// private Frame presenter later; visual-tree-dependent initialization belongs in Loaded,
    /// <see cref="IAsyncNavigationAware.OnNavigationEnteredAsync"/>, or
    /// <see cref="ICancellableAsyncNavigationAware.OnNavigationEnteredAsync(CancellationToken)"/>.
    /// </remarks>
    public event TypedEventHandler<NavigationView, NavigatedEventArgs> Navigated
    {
        add => AddHandler(NavigatedEvent, value);
        remove => RemoveHandler(NavigatedEvent, value);
    }

    #endregion

    /// <summary>
    /// Occurs when a navigation lifecycle callback fails.
    /// </summary>
    /// <remarks>
    /// Navigation is already committed when this event is raised and cannot be rolled back. The
    /// event is the explicit observation boundary for synchronous and asynchronous lifecycle
    /// callbacks. If no handler marks <see cref="NavigationCallbackFailedEventArgs.Handled"/>, the
    /// contextual exception is rethrown on this control's dispatcher.
    /// </remarks>
    public event EventHandler<NavigationCallbackFailedEventArgs>? NavigationCallbackFailed;

    /// <summary>
    /// Raises the pane opened event.
    /// </summary>
    protected virtual void OnPaneOpened()
    {
        RaiseEvent(new NavigationViewRoutedEventArgs(PaneOpenedEvent, this));
    }

    /// <summary>
    /// Raises the pane closed event.
    /// </summary>
    protected virtual void OnPaneClosed()
    {
        RaiseEvent(new NavigationViewRoutedEventArgs(PaneClosedEvent, this));
    }

    /// <summary>
    /// Raises the selection changed event.
    /// </summary>
    protected virtual void OnSelectionChanged()
    {
        RaiseEvent(new NavigationViewRoutedEventArgs(SelectionChangedEvent, this));
    }

    /// <summary>
    /// Raises the item invoked event.
    /// </summary>
    protected virtual void OnItemInvoked()
    {
        RaiseEvent(new NavigationViewRoutedEventArgs(ItemInvokedEvent, this));
    }

    /// <summary>
    /// Raises the back requested event.
    /// </summary>
    protected virtual void OnBackRequested()
    {
        RaiseEvent(new NavigationViewRoutedEventArgs(BackRequestedEvent, this));
    }

    /// <summary>
    /// Raises the navigating requested event.
    /// </summary>
    protected virtual bool OnNavigating(object sourcePage)
    {
        var eventArgs = new NavigatingEventArgs(NavigatingEvent, this) { Page = sourcePage };

        RaiseEvent(eventArgs);

        return eventArgs.Cancel;
    }

    /// <summary>
    /// Raises the navigated requested event.
    /// </summary>
    protected virtual void OnNavigated(object page)
    {
        var eventArgs = new NavigatedEventArgs(NavigatedEvent, this) { Page = page };

        RaiseEvent(eventArgs);
    }

    /// <summary>Raises <see cref="NavigationCallbackFailed"/> and applies fail-fast fallback.</summary>
    /// <param name="eventArgs">The contextual callback failure.</param>
    /// <remarks>
    /// Derived controls may override this method to integrate lifecycle failures with an
    /// application-wide error surface. An override should call the base implementation when it
    /// wants the public event and default unhandled-exception behavior.
    /// </remarks>
    protected virtual void OnNavigationCallbackFailed(NavigationCallbackFailedEventArgs eventArgs)
    {
        NavigationCallbackFailed?.Invoke(this, eventArgs);

        if (!eventArgs.Handled)
        {
            throw eventArgs.Exception;
        }
    }
}
