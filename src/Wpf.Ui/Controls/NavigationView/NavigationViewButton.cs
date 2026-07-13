// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a command button owned by a <see cref="NavigationView"/>.
/// </summary>
public class NavigationViewButton : ButtonBase, IIconControl
{
    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(NavigationViewButton),
        new PropertyMetadata(null, null, IconElement.Coerce)
    );

    /// <summary>
    /// Gets or sets the icon displayed by the button.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen),
        typeof(bool),
        typeof(NavigationViewButton),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the owning navigation pane is open.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

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

    private static readonly DependencyPropertyKey PlacementPropertyKey = DependencyProperty.RegisterReadOnly(
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

    private static readonly DependencyPropertyDescriptor PaneDisplayModeDescriptor =
        DependencyPropertyDescriptor.FromProperty(
            NavigationView.PaneDisplayModeProperty,
            typeof(NavigationView)
        );

    private NavigationView? _ownerNavigationView;
    private NavigationView? _attachedNavigationView;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneOpenedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneClosedHandler;
    private EventHandler?   _paneDisplayModeChangedHandler;

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

    internal void InitializeOwner(NavigationView owner, NavigationViewButtonPlacement placement)
    {
        _ownerNavigationView = owner;
        SetValue(PlacementPropertyKey, placement);
        AttachNavigationViewEvents(owner);
    }

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

        navigationView.PaneOpened += _paneOpenedHandler;
        navigationView.PaneClosed += _paneClosedHandler;
        PaneDisplayModeDescriptor.AddValueChanged(navigationView, _paneDisplayModeChangedHandler);
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
            PaneDisplayModeDescriptor.RemoveValueChanged(
                _attachedNavigationView,
                _paneDisplayModeChangedHandler
            );
            _paneDisplayModeChangedHandler = null;
        }

        _attachedNavigationView = null;
    }

    private void SynchronizePaneState(NavigationView navigationView)
    {
        SetValue(PaneDisplayModePropertyKey, navigationView.PaneDisplayMode);
        SetCurrentValue(IsPaneOpenProperty, navigationView.IsPaneOpen);
    }
}
