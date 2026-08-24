// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a command button owned by a <see cref="NavigationView"/>.
/// </summary>
public class NavigationViewButton : ButtonBase
{
    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(NavigationViewButton),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>Identifies the <see cref="IconMargin"/> dependency property.</summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(NavigationViewButton),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

    #region DP: PaneTitle

    private static readonly DependencyPropertyKey PaneTitlePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneTitle),
            typeof(string),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="PaneTitle"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="null"/>. The property carries state only: changing it
    /// does not invalidate layout or rendering unless a template or style consumes the value.
    /// </remarks>
    public static readonly DependencyProperty PaneTitleProperty = PaneTitlePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the pane title synchronized from the owning <see cref="NavigationView"/>.
    /// </summary>
    /// <remarks>
    /// This property exposes navigation state to templates without reserving
    /// <see cref="ContentControl.Content"/>.
    /// A template may present the value with an <see cref="AccessText"/>, a <see cref="TextBlock"/>,
    /// or any application-specific content pipeline.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public string? PaneTitle => (string?)GetValue(PaneTitleProperty);

    #endregion

    #region DP: IsPaneOpen

    private static readonly DependencyPropertyKey IsPaneOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPaneOpen),
            typeof(bool),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty =
        IsPaneOpenPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the owning navigation pane is open.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen => (bool)GetValue(IsPaneOpenProperty);

    #endregion

    #region DP: PaneDisplayMode

    private static readonly DependencyPropertyKey PaneDisplayModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneDisplayMode),
            typeof(NavigationViewPaneDisplayMode),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(NavigationViewPaneDisplayMode.Left)
        );

    /// <summary>Identifies the <see cref="PaneDisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty PaneDisplayModeProperty =
        PaneDisplayModePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the pane display mode synchronized from the owning <see cref="NavigationView"/>.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public NavigationViewPaneDisplayMode PaneDisplayMode =>
        (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);

    #endregion

    #region DP: Placement

    private static readonly DependencyPropertyKey PlacementPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Placement),
            typeof(NavigationViewButtonPlacement),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(NavigationViewButtonPlacement.None)
        );

    /// <summary>Identifies the <see cref="Placement"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementProperty = PlacementPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the host in which this button is placed.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public NavigationViewButtonPlacement Placement =>
        (NavigationViewButtonPlacement)GetValue(PlacementProperty);

    #endregion

    #region DP: Role

    private static readonly DependencyPropertyKey RolePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Role),
            typeof(NavigationViewButtonRole),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(NavigationViewButtonRole.None)
        );

    /// <summary>
    /// Identifies the read-only <see cref="Role"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="NavigationViewButtonRole.None"/>. The property carries
    /// template state and does not invalidate layout or rendering by itself.
    /// </remarks>
    public static readonly DependencyProperty RoleProperty = RolePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the navigation command represented by this button.
    /// </summary>
    /// <remarks>
    /// Templates use the role together with <see cref="Placement"/> to select host-specific
    /// presentation without relying on element names, automation identifiers, or icon instances.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public NavigationViewButtonRole Role => (NavigationViewButtonRole)GetValue(RoleProperty);

    #endregion

    private NavigationView? _ownerNavigationView;
    private NavigationView? _attachedNavigationView;

    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneOpenedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneClosedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneDisplayModeChangedHandler;
    private EventHandler?                                       _paneTitleChangedHandler;

    static NavigationViewButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(typeof(NavigationViewButton))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewButton"/> class.
    /// </summary>
    public NavigationViewButton()
    {
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal void InitializeOwner(
        NavigationView owner,
        NavigationViewButtonPlacement placement,
        NavigationViewButtonRole role)
    {
        _ownerNavigationView = owner;
        SetValue(PlacementPropertyKey, placement);
        SetValue(RolePropertyKey,      role);
        AttachNavigationViewEvents(owner);
    }

    /// <summary>
    /// Synchronizes the title independently from <see cref="ContentControl.Content"/> so a template
    /// can consume navigation state without NavigationView taking ownership of the button's content
    /// pipeline.
    /// </summary>
    internal void SynchronizePaneTitle(NavigationView navigationView) =>
        SetValue(PaneTitlePropertyKey, navigationView.PaneTitle);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var navigationView = _ownerNavigationView ?? NavigationView.GetNavigationParent(this);
        if (navigationView is not null)
        {
            AttachNavigationViewEvents(navigationView);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachNavigationViewEvents();
    }

    private void AttachNavigationViewEvents(NavigationView navigationView)
    {
        if (ReferenceEquals(_attachedNavigationView, navigationView))
        {
            SynchronizePaneState(navigationView);
            return;
        }

        DetachNavigationViewEvents();
        _attachedNavigationView = navigationView;
        SynchronizePaneState(navigationView);

        // Pane notifications bubble through nested NavigationView instances. The button currently
        // reads state back from its owner, so handling a descendant notification cannot normally
        // copy the descendant's values; nevertheless, accepting it performs an unrelated state
        // write and leaves this synchronization path vulnerable if event-specific state is added
        // later. Treat the owning view as the instance boundary, consistently with NavigationViewItem.
        _paneOpenedHandler             = (_, args) => SynchronizePaneStateFromOwnerEvent(navigationView, args);
        _paneClosedHandler             = (_, args) => SynchronizePaneStateFromOwnerEvent(navigationView, args);
        _paneDisplayModeChangedHandler = (_, args) => SynchronizePaneStateFromOwnerEvent(navigationView, args);
        _paneTitleChangedHandler       = (_, _)    => SynchronizePaneTitle(navigationView);

        navigationView.PaneOpened               += _paneOpenedHandler;
        navigationView.PaneClosed               += _paneClosedHandler;
        navigationView.PaneDisplayModeChanged   += _paneDisplayModeChangedHandler;
        navigationView.PaneTitleChangedInternal += _paneTitleChangedHandler;
    }

    private void DetachNavigationViewEvents()
    {
        if (_attachedNavigationView is null)
        {
            return;
        }

        if (_paneOpenedHandler is not null)
        {
            _attachedNavigationView.PaneOpened -= _paneOpenedHandler;
            _paneOpenedHandler                 =  null;
        }

        if (_paneClosedHandler is not null)
        {
            _attachedNavigationView.PaneClosed -= _paneClosedHandler;
            _paneClosedHandler                 =  null;
        }

        if (_paneDisplayModeChangedHandler is not null)
        {
            _attachedNavigationView.PaneDisplayModeChanged -= _paneDisplayModeChangedHandler;
            _paneDisplayModeChangedHandler                 =  null;
        }

        if (_paneTitleChangedHandler is not null)
        {
            _attachedNavigationView.PaneTitleChangedInternal -= _paneTitleChangedHandler;
            _paneTitleChangedHandler                         =  null;
        }

        _attachedNavigationView = null;
    }

    /// <summary>
    /// Refreshes button presentation only for a notification raised by the button's owning view.
    /// Public pane events remain bubbling events, while internal owner synchronization stays local
    /// when one <see cref="NavigationView"/> is hosted inside another.
    /// </summary>
    private void SynchronizePaneStateFromOwnerEvent(
        NavigationView  navigationView,
        RoutedEventArgs args)
    {
        if (!ReferenceEquals(args.OriginalSource, navigationView))
        {
            return;
        }

        SynchronizePaneState(navigationView);
    }

    private void SynchronizePaneState(NavigationView navigationView)
    {
        SetValue(PaneDisplayModePropertyKey, navigationView.PaneDisplayMode);
        SetValue(IsPaneOpenPropertyKey,      navigationView.IsPaneOpen);
        SynchronizePaneTitle(navigationView);
    }
}
