// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a command button owned by a <see cref="NavigationView"/>.
/// </summary>
public class NavigationViewButton : ButtonBase
{
    #region DP: Icon

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The property accepts any object and defaults to <see langword="null"/>. It carries content
    /// for the template's icon slot and does not invalidate layout or rendering by itself; the
    /// active template determines how a value is presented.
    /// </remarks>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(NavigationViewButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the content presented in the button's icon slot.
    /// </summary>
    /// <remarks>
    /// The value is intentionally not restricted to <see cref="IconElement"/>. Applications may
    /// supply a visual element, a view-model resolved by an implicit data template, text, or any
    /// other content understood by their control template. When the value is <see langword="null"/>,
    /// the default template selects its built-in glyph from <see cref="Role"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
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

        _paneOpenedHandler             = (_, _) => SynchronizePaneState(navigationView);
        _paneClosedHandler             = (_, _) => SynchronizePaneState(navigationView);
        _paneDisplayModeChangedHandler = (_, _) => SynchronizePaneState(navigationView);
        _paneTitleChangedHandler       = (_, _) => SynchronizePaneTitle(navigationView);

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

    private void SynchronizePaneState(NavigationView navigationView)
    {
        SetValue(PaneDisplayModePropertyKey, navigationView.PaneDisplayMode);
        SetValue(IsPaneOpenPropertyKey,      navigationView.IsPaneOpen);
        SynchronizePaneTitle(navigationView);
    }
}