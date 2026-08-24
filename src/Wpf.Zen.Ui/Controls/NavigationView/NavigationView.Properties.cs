// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls;
using Wpf.Zen.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines the dependency properties and dp callbacks for <see cref="NavigationView"/> control
/// </content>
[StyleTypedProperty(Property = nameof(MenuItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
[StyleTypedProperty(Property = nameof(FooterMenuItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public partial class NavigationView
{
    #region Attached DP: IsNavigationFrameScrollEnabled

    /// <summary>
    /// Identifies the <c>NavigationView.IsNavigationFrameScrollEnabled</c> attached dependency
    /// property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. Set this property on a navigation content root
    /// to request that its owning <see cref="NavigationView"/> place the stable navigation frame
    /// inside a <see cref="DynamicScrollViewer"/>. The navigation view inspects only the root before
    /// passing it to WPF's <see cref="Frame"/>; the value is not inherited and changing it after a
    /// navigation has committed does not retroactively replace the host layout.
    ///
    /// This contract is deliberately independent from <see cref="ScrollViewer.CanContentScroll"/>.
    /// That WPF property continues to select logical or physical scrolling on the actual scroll
    /// viewer and must not also control whether NavigationView creates a viewer.
    /// </remarks>
    public static readonly DependencyProperty IsNavigationFrameScrollEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsNavigationFrameScrollEnabled",
            typeof(bool),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Gets whether <paramref name="element"/> requests scrolling from its navigation frame.
    /// </summary>
    /// <param name="element">The navigation content root to read.</param>
    /// <returns><see langword="true"/> when the navigation frame should be scrollable.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static bool GetIsNavigationFrameScrollEnabled(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(IsNavigationFrameScrollEnabledProperty);
    }

    /// <summary>
    /// Sets whether <paramref name="element"/> requests scrolling from its navigation frame.
    /// </summary>
    /// <param name="element">The navigation content root to configure.</param>
    /// <param name="value">
    /// <see langword="true"/> to wrap the navigation frame in the host's dynamic scroll viewer;
    /// otherwise, <see langword="false"/>.
    /// </param>
    public static void SetIsNavigationFrameScrollEnabled(DependencyObject element, bool value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IsNavigationFrameScrollEnabledProperty, value);
    }

    #endregion

    #region Read-only DP: IsNavigationFrameScrollActive

    private static readonly DependencyPropertyKey IsNavigationFrameScrollActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsNavigationFrameScrollActive),
            typeof(bool),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only <see cref="IsNavigationFrameScrollActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsNavigationFrameScrollActiveProperty =
        IsNavigationFrameScrollActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the current navigation frame is hosted by the built-in
    /// dynamic scroll viewer.
    /// </summary>
    /// <remarks>
    /// This value is derived from <see cref="IsNavigationFrameScrollEnabledProperty"/> on the
    /// destination root before WPF receives the navigation request. It is read-only so custom
    /// templates can select a presentation without allowing application code to create a layout
    /// state that disagrees with the current page contract.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsNavigationFrameScrollActive =>
        (bool)GetValue(IsNavigationFrameScrollActiveProperty);

    #endregion

    #region DP: AlwaysShowHeader

    /// <summary>Identifies the <see cref="AlwaysShowHeader"/> dependency property.</summary>
    public static readonly DependencyProperty AlwaysShowHeaderProperty = DependencyProperty.Register(
        nameof(AlwaysShowHeader),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Gets or sets a value indicating whether the header remains visible when no header content is available.</summary>
    public bool AlwaysShowHeader
    {
        get => (bool)GetValue(AlwaysShowHeaderProperty);
        set => SetValue(AlwaysShowHeaderProperty, value);
    }

    #endregion

    #region DP: AutoSuggestBox

    /// <summary>Identifies the <see cref="AutoSuggestBox"/> dependency property.</summary>
    public static readonly DependencyProperty AutoSuggestBoxProperty = DependencyProperty.Register(
        nameof(AutoSuggestBox),
        typeof(AutoSuggestBox),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnAutoSuggestBoxChanged)
    );

    /// <summary>Gets or sets the suggestion box displayed in the navigation pane.</summary>
    public AutoSuggestBox? AutoSuggestBox
    {
        get => (AutoSuggestBox?)GetValue(AutoSuggestBoxProperty);
        set => SetValue(AutoSuggestBoxProperty, value);
    }

    private static void OnAutoSuggestBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if (e.OldValue is AutoSuggestBox oldValue)
        {
            oldValue.SuggestionChosen -= navigationView.AutoSuggestBoxOnSuggestionChosen;
            oldValue.QuerySubmitted -= navigationView.AutoSuggestBoxOnQuerySubmitted;

            // OriginalItemsSource is owned by NavigationView only while the box is associated with
            // this control. Releasing it here prevents a replaced box from retaining the complete
            // navigation index through the private observable projection.
            if (ReferenceEquals(oldValue.OriginalItemsSource, navigationView._autoSuggestItems))
            {
                oldValue.ClearValue(AutoSuggestBox.OriginalItemsSourceProperty);
            }
        }

        if (e.NewValue is not AutoSuggestBox autoSuggestBox)
        {
            return;
        }

        autoSuggestBox.OriginalItemsSource = navigationView._autoSuggestItems;
        navigationView.UpdateAutoSuggestBoxSuggestions();

        // The property can change while the control is unloaded. Event handlers are attached only
        // for a live presentation; OnLoaded establishes the same pair after ordinary reparenting.
        if (navigationView.IsLoaded)
        {
            autoSuggestBox.SuggestionChosen += navigationView.AutoSuggestBoxOnSuggestionChosen;
            autoSuggestBox.QuerySubmitted += navigationView.AutoSuggestBoxOnQuerySubmitted;
        }
    }

    #endregion

    #region DP: NavigationMap

    /// <summary>Identifies the <see cref="NavigationMap"/> dependency property.</summary>
    /// <remarks>
    /// The property does not affect layout or rendering directly. Its change callback only moves
    /// map notifications and resolves the active semantic location again. A unique map is assigned
    /// by each NavigationView constructor so the dependency-property default is never shared.
    /// </remarks>
    public static readonly DependencyProperty NavigationMapProperty = DependencyProperty.Register(
        nameof(NavigationMap),
        typeof(NavigationMap),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnNavigationMapChanged)
    );

    /// <summary>
    /// Gets or sets the semantic route graph used to derive default breadcrumb paths.
    /// </summary>
    /// <remarks>
    /// This map is independent from <see cref="MenuItemsSource"/> and
    /// <see cref="FooterMenuItemsSource"/>. Reordering a menu therefore never silently reparents a
    /// page, while mutating this map immediately updates the active location. The map can be
    /// assigned from code, a resource, or declared inline with <see cref="NavigationMapNode"/>
    /// children in XAML.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Maintainability",
        "WPF0012:CLR property type should match registered dependency-property nullability",
        Justification = "The constructor assigns a unique map before the control is exposed; nullable metadata avoids a process-wide shared mutable default."
    )]
    public NavigationMap NavigationMap
    {
        get => (NavigationMap?)GetValue(NavigationMapProperty)
            ?? throw new InvalidOperationException("The navigation map has not been initialized.");
        set => SetValue(NavigationMapProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnNavigationMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            NavigationMap? oldMap = e.OldValue as NavigationMap;
            NavigationMap? newMap = e.NewValue as NavigationMap;
            navigationView.ChangeNavigationMap(oldMap, newMap);

            // ClearValue and a temporarily null binding must not leave INavigationState with an
            // unusable map. SetCurrentValue preserves an existing binding while assigning a unique
            // empty graph; the nested callback attaches that replacement normally.
            if (newMap is null)
            {
                navigationView.SetCurrentValue(NavigationMapProperty, new NavigationMap());
            }
        }
    }

    #endregion

    #region Read-only DP: CurrentLocation

    private static readonly DependencyPropertyKey CurrentLocationPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentLocation),
            typeof(NavigationLocation),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="CurrentLocation"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentLocationProperty =
        CurrentLocationPropertyKey.DependencyProperty;

    /// <summary>Gets the current semantic location, or <see langword="null"/> before navigation.</summary>
    public NavigationLocation? CurrentLocation =>
        (NavigationLocation?)GetValue(CurrentLocationProperty);

    #endregion

    #region DP: LeftPaneCompactWidth

    /// <summary>
    /// Identifies the <see cref="LeftPaneCompactWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftPaneCompactWidthProperty = DependencyProperty.Register(
        nameof(LeftPaneCompactWidth),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D, OnLeftPaneTargetWidthChanged)
    );

    /// <summary>
    /// Gets or sets the width used by the built-in Left pane while <see cref="IsPaneOpen"/> is
    /// <see langword="false"/>. The value is also used as the fixed width of the current
    /// LeftMinimal template. Changing it while the compact Left pane is visible updates the pane
    /// immediately rather than replaying an open/close animation.
    /// </summary>
    public double LeftPaneCompactWidth
    {
        get => (double)GetValue(LeftPaneCompactWidthProperty);
        set => SetValue(LeftPaneCompactWidthProperty, value);
    }

    #endregion

    #region Collection: FooterMenuItems

    /// <summary>
    /// Gets the collection of items displayed in the footer menu.
    /// </summary>
    /// <remarks>
    /// The collection instance is owned by the control and remains stable for its lifetime. It is
    /// deliberately a CLR collection property rather than a read-only dependency property. WPF's
    /// designer gives dependency-property metadata precedence over the CLR collection contract;
    /// a read-only dependency property can therefore be rejected as inaccessible even though the
    /// runtime XAML loader can populate the returned collection. This follows
    /// <see cref="ItemsControl.Items"/>: XAML adds children to the existing collection instead of
    /// assigning a replacement value.
    /// </remarks>
    [Bindable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableCollection<object> FooterMenuItems { get; } = [];

    #endregion

    #region DP: FooterMenuItemsSource

    /// <summary>Identifies the <see cref="FooterMenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemsSourceProperty = DependencyProperty.Register(
        nameof(FooterMenuItemsSource),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnFooterMenuItemsSourceChanged)
    );

    /// <summary>Gets or sets the source used to generate footer menu items.</summary>
    [Bindable(true)]
    public object? FooterMenuItemsSource
    {
        get => GetValue(FooterMenuItemsSourceProperty);
        set => SetValue(FooterMenuItemsSourceProperty, value);
    }

    private static void OnFooterMenuItemsSourceChanged(
        DependencyObject? d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.UpdateMenuItemsHosts();
        navigationView.RebuildNavigationIndexes();
    }

    #endregion

    #region DP: FrameMargin

    /// <summary>Identifies the <see cref="FrameMargin"/> dependency property.</summary>
    public static readonly DependencyProperty FrameMarginProperty = DependencyProperty.Register(
        nameof(FrameMargin),
        typeof(Thickness),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(default(Thickness))
    );

    /// <summary>Gets or sets the margin applied around the navigation frame.</summary>
    public Thickness FrameMargin
    {
        get => (Thickness)GetValue(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    #endregion

    #region DP: HasFooterMenuItems

    private static readonly DependencyPropertyKey HasFooterMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasFooterMenuItems),
            typeof(bool),
            typeof(NavigationView),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="HasFooterMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasFooterMenuItemsProperty =
        HasFooterMenuItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether <see cref="FooterMenuItems"/> contains at least one item.
    /// </summary>
    /// <remarks>
    /// The value is maintained by the control whenever the internal footer collection changes, including
    /// changes synchronized from <see cref="FooterMenuItemsSource"/>. As a read-only dependency property,
    /// it can be consumed by bindings and template triggers without allowing callers to create a state that
    /// disagrees with the collection.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasFooterMenuItems => (bool)GetValue(HasFooterMenuItemsProperty);

    #endregion

    #region DP: HasMenuItems

    private static readonly DependencyPropertyKey HasMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasMenuItems),
            typeof(bool),
            typeof(NavigationView),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasMenuItemsProperty =
        HasMenuItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether <see cref="MenuItems"/> contains at least one item.
    /// </summary>
    /// <remarks>
    /// The value is maintained by the control whenever the internal menu collection changes, including
    /// changes synchronized from <see cref="MenuItemsSource"/>. As a read-only dependency property, it can
    /// be consumed by bindings and template triggers without allowing callers to create a state that
    /// disagrees with the collection.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasMenuItems => (bool)GetValue(HasMenuItemsProperty);

    #endregion

    #region DP: Header

    /// <summary>Identifies the <see cref="Header"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets the content displayed in the page header.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    #endregion

    #region DP: HeaderVisibility

    /// <summary>Identifies the <see cref="HeaderVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderVisibilityProperty = DependencyProperty.Register(
        nameof(HeaderVisibility),
        typeof(Visibility),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(Visibility.Visible)
    );

    /// <summary>Gets or sets the visibility of the page header.</summary>
    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    #endregion

    #region DP: SuppressedNavigationInput

    /// <summary>
    /// Identifies the <see cref="SuppressedNavigationInput"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SuppressedNavigationInputProperty = DependencyProperty.Register(
        nameof(SuppressedNavigationInput),
        typeof(NavigationViewInput),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewInput.None),
        validateValueCallback: value
            => value is NavigationViewInput input && (input & ~NavigationViewInput.All) == 0
    );

    /// <summary>
    /// Gets or sets the legacy browser-navigation input that is stopped at this control boundary.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="NavigationViewInput.None"/>: a page remains free to use F5
    /// or mouse side buttons for application-specific commands. Set this property to
    /// <see cref="NavigationViewInput.All"/> to suppress the old WPF browser refresh, back, and
    /// forward input model. Suppression covers raw routed input, the standard
    /// <see cref="System.Windows.Input.NavigationCommands"/>, and commands generated from
    /// <c>WM_APPCOMMAND</c>; it does not affect explicit calls to <see cref="GoBack"/> or the
    /// built-in back button.
    /// </remarks>
    public NavigationViewInput SuppressedNavigationInput
    {
        get => (NavigationViewInput)GetValue(SuppressedNavigationInputProperty);
        set => SetValue(SuppressedNavigationInputProperty, value);
    }

    #endregion

    #region DP: IsBackButtonVisible

    /// <summary>
    /// Identifies the <see cref="IsBackButtonVisible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsBackButtonVisibleProperty = DependencyProperty.Register(
        nameof(IsBackButtonVisible),
        typeof(NavigationViewBackButtonVisible),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewBackButtonVisible.Auto, OnPaneButtonVisibilityChanged)
    );

    /// <summary>
    /// Gets or sets the policy that controls whether the back button is shown.
    /// </summary>
    /// <remarks>
    /// <see cref="NavigationViewBackButtonVisible.Visible"/> keeps the button visible and lets
    /// <see cref="IsBackEnabled"/> communicate whether it can be invoked.
    /// <see cref="NavigationViewBackButtonVisible.Collapsed"/> removes it from layout, while
    /// <see cref="NavigationViewBackButtonVisible.Auto"/> shows it only when the navigation journal
    /// can go back. The default value is <see cref="NavigationViewBackButtonVisible.Auto"/>.
    /// </remarks>
    public NavigationViewBackButtonVisible IsBackButtonVisible
    {
        get => (NavigationViewBackButtonVisible)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    #endregion

    #region Read-only DP: IsBackEnabled

    private static readonly DependencyPropertyKey IsBackEnabledPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsBackEnabled),
            typeof(bool),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(false, OnIsBackEnabledChanged)
        );

    /// <summary>Identifies the <see cref="IsBackEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsBackEnabledProperty =
        IsBackEnabledPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the navigation journal currently permits a back operation.
    /// </summary>
    /// <remarks>
    /// This is navigation capability rather than presentation policy. It remains independent of
    /// <see cref="IsBackButtonVisible"/> so bindings and automation do not report that navigation is
    /// unavailable merely because its built-in button was hidden. The value is maintained by the
    /// control whenever its journal changes.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsBackEnabled => (bool)GetValue(IsBackEnabledProperty);

    private static void OnIsBackEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.UpdateBackButtonVisibility();
        }
    }

    #endregion

    #region DP: IsFooterSeparatorVisible

    /// <summary>Identifies the <see cref="IsFooterSeparatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsFooterSeparatorVisibleProperty = DependencyProperty.Register(
        nameof(IsFooterSeparatorVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true)
    );

    /// <summary>Gets or sets a value indicating whether the separator above the footer menu is visible.</summary>
    public bool IsFooterSeparatorVisible
    {
        get => (bool)GetValue(IsFooterSeparatorVisibleProperty);
        set => SetValue(IsFooterSeparatorVisibleProperty, value);
    }

    #endregion

    #region DP: IsPaneOpen

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true, OnIsPaneOpenChanged)
    );

    /// <summary>Gets or sets a value indicating whether the navigation pane is expanded to its open width.</summary>
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if ((bool)e.NewValue == (bool)e.OldValue)
        {
            return;
        }

        navigationView.SetValue(IsPaneOpenContextProperty, (bool)e.NewValue);
        navigationView.CloseAllNavigationViewItemSubmenus();

        if (navigationView.IsPaneOpen)
        {
            navigationView.OnPaneOpened();
        }
        else
        {
            navigationView.OnPaneClosed();
        }

        navigationView.UpdatePaneButtonPlacement();
        UpdateVisualState(navigationView);
    }

    #endregion

    #region DP: IsPaneToggleVisible

    /// <summary>Identifies the <see cref="IsPaneToggleVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneToggleVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneToggleVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true, OnPaneButtonVisibilityChanged)
    );

    /// <summary>Gets or sets a value indicating whether the pane toggle button is visible.</summary>
    public bool IsPaneToggleVisible
    {
        get => (bool)GetValue(IsPaneToggleVisibleProperty);
        set => SetValue(IsPaneToggleVisibleProperty, value);
    }

    /// <summary>
    /// Shared callback for <see cref="IsBackButtonVisibleProperty"/> and
    /// <see cref="IsPaneToggleVisibleProperty"/>.
    /// </summary>
    private static void OnPaneButtonVisibilityChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is NavigationView navigationView)
        {
            // Back-button visibility is applied to two control-owned instances, one of which may
            // live in TitleBar.LeftContent outside this control's visual tree. Update those elements
            // directly before recomputing their hosts instead of relying on a template ancestor.
            navigationView.UpdateBackButtonVisibility();
            navigationView.UpdatePaneButtonPlacement();
        }
    }

    #endregion

    #region DP: IsPaneVisible

    /// <summary>Identifies the <see cref="IsPaneVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Gets or sets a value indicating whether the navigation pane is displayed.</summary>
    public bool IsPaneVisible
    {
        get => (bool)GetValue(IsPaneVisibleProperty);
        set => SetValue(IsPaneVisibleProperty, value);
    }

    #endregion

    #region DP: IsTopSeparatorVisible

    /// <summary>Identifies the <see cref="IsTopSeparatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsTopSeparatorVisibleProperty = DependencyProperty.Register(
        nameof(IsTopSeparatorVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true)
    );

    /// <summary>Gets or sets a value indicating whether the separator above the primary menu is visible.</summary>
    public bool IsTopSeparatorVisible
    {
        get => (bool)GetValue(IsTopSeparatorVisibleProperty);
        set => SetValue(IsTopSeparatorVisibleProperty, value);
    }

    #endregion

    #region DP: MenuItemContainerStyle

    /// <summary>Identifies the <see cref="MenuItemContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemContainerStyleProperty =
        DependencyProperty.Register(
            nameof(MenuItemContainerStyle),
            typeof(Style),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the default style applied to <see cref="NavigationViewItem"/> containers in the
    /// complete primary-menu hierarchy rooted at <see cref="MenuItems"/> or
    /// <see cref="MenuItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. This property configures only the primary menu host;
    /// use <see cref="FooterMenuItemContainerStyle"/> to style footer item containers independently.
    /// Both generated containers and directly supplied items receive the style at every depth. A
    /// <see cref="ItemsControl.ItemContainerStyle"/> set on an individual item takes precedence
    /// for that item's descendants, matching the standard WPF branch-override model.
    /// </remarks>
    public Style? MenuItemContainerStyle
    {
        get => (Style?)GetValue(MenuItemContainerStyleProperty);
        set => SetValue(MenuItemContainerStyleProperty, value);
    }

    #endregion

    #region DP: MenuItemContainerStyleSelector

    /// <summary>Identifies the <see cref="MenuItemContainerStyleSelector"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemContainerStyleSelectorProperty =
        DependencyProperty.Register(
            nameof(MenuItemContainerStyleSelector),
            typeof(StyleSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the selector that chooses a container style for each primary menu item.
    /// </summary>
    /// <remarks>
    /// The selector is used throughout the primary-menu hierarchy. WPF consults it when
    /// <see cref="MenuItemContainerStyle"/> is <see langword="null"/>; use
    /// <see cref="FooterMenuItemContainerStyleSelector"/> for footer-specific selection.
    /// </remarks>
    public StyleSelector? MenuItemContainerStyleSelector
    {
        get => (StyleSelector?)GetValue(MenuItemContainerStyleSelectorProperty);
        set => SetValue(MenuItemContainerStyleSelectorProperty, value);
    }

    #endregion

    #region DP: FooterMenuItemContainerStyle

    /// <summary>Identifies the <see cref="FooterMenuItemContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemContainerStyleProperty =
        DependencyProperty.Register(
            nameof(FooterMenuItemContainerStyle),
            typeof(Style),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the default style applied to <see cref="NavigationViewItem"/> containers in the
    /// complete footer-menu hierarchy rooted at <see cref="FooterMenuItems"/> or
    /// <see cref="FooterMenuItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. Keeping this value separate from
    /// <see cref="MenuItemContainerStyle"/> lets templates present the fixed footer region without a
    /// selector that must infer which list owns an item. An individual item's
    /// <see cref="ItemsControl.ItemContainerStyle"/> remains a branch-local override.
    /// </remarks>
    public Style? FooterMenuItemContainerStyle
    {
        get => (Style?)GetValue(FooterMenuItemContainerStyleProperty);
        set => SetValue(FooterMenuItemContainerStyleProperty, value);
    }

    #endregion

    #region DP: FooterMenuItemContainerStyleSelector

    /// <summary>
    /// Identifies the <see cref="FooterMenuItemContainerStyleSelector"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterMenuItemContainerStyleSelectorProperty =
        DependencyProperty.Register(
            nameof(FooterMenuItemContainerStyleSelector),
            typeof(StyleSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the selector that chooses a container style for each footer menu item.
    /// </summary>
    /// <remarks>
    /// The selector is used throughout the footer-menu hierarchy. WPF consults it when
    /// <see cref="FooterMenuItemContainerStyle"/> is <see langword="null"/>.
    /// </remarks>
    public StyleSelector? FooterMenuItemContainerStyleSelector
    {
        get => (StyleSelector?)GetValue(FooterMenuItemContainerStyleSelectorProperty);
        set => SetValue(FooterMenuItemContainerStyleSelectorProperty, value);
    }

    #endregion

    #region DP: MenuItemTemplate

    /// <summary>Identifies the <see cref="MenuItemTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemTemplateProperty = DependencyProperty.Register(
        nameof(MenuItemTemplate),
        typeof(DataTemplate),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <summary>
    /// Gets or sets the template used to present data items in <see cref="MenuItems"/> or
    /// <see cref="MenuItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. The template is forwarded only to the primary menu
    /// host and affects measurement when changed. It supplies the generated
    /// <see cref="HeaderedItemsControl.HeaderTemplate"/> for data items; directly supplied UI elements
    /// continue to own their presentation according to normal WPF item-container semantics.
    /// </remarks>
    public DataTemplate? MenuItemTemplate
    {
        get => (DataTemplate?)GetValue(MenuItemTemplateProperty);
        set => SetValue(MenuItemTemplateProperty, value);
    }

    #endregion

    #region DP: MenuItemTemplateSelector

    /// <summary>Identifies the <see cref="MenuItemTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(MenuItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the selector that chooses a data template for each primary menu item.
    /// </summary>
    /// <remarks>
    /// The selector is forwarded only to the <see cref="MenuItems"/> host. WPF consults it when
    /// <see cref="MenuItemTemplate"/> is <see langword="null"/>; use
    /// <see cref="FooterMenuItemTemplateSelector"/> for footer-specific selection.
    /// </remarks>
    public DataTemplateSelector? MenuItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(MenuItemTemplateSelectorProperty);
        set => SetValue(MenuItemTemplateSelectorProperty, value);
    }

    #endregion

    #region DP: FooterMenuItemTemplate

    /// <summary>Identifies the <see cref="FooterMenuItemTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemTemplateProperty = DependencyProperty.Register(
        nameof(FooterMenuItemTemplate),
        typeof(DataTemplate),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <summary>
    /// Gets or sets the template used to present data items in <see cref="FooterMenuItems"/> or
    /// <see cref="FooterMenuItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. The template is forwarded only to the footer menu
    /// host and affects measurement when changed, allowing its data presentation to differ from
    /// the primary menu without requiring a selector to identify the owning list.
    /// </remarks>
    public DataTemplate? FooterMenuItemTemplate
    {
        get => (DataTemplate?)GetValue(FooterMenuItemTemplateProperty);
        set => SetValue(FooterMenuItemTemplateProperty, value);
    }

    #endregion

    #region DP: FooterMenuItemTemplateSelector

    /// <summary>
    /// Identifies the <see cref="FooterMenuItemTemplateSelector"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterMenuItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(FooterMenuItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the selector that chooses a data template for each footer menu item.
    /// </summary>
    /// <remarks>
    /// The selector is forwarded only to the <see cref="FooterMenuItems"/> host. WPF consults it
    /// when <see cref="FooterMenuItemTemplate"/> is <see langword="null"/>.
    /// </remarks>
    public DataTemplateSelector? FooterMenuItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(FooterMenuItemTemplateSelectorProperty);
        set => SetValue(FooterMenuItemTemplateSelectorProperty, value);
    }

    #endregion

    #region Collection: MenuItems

    /// <summary>
    /// Gets the collection of items displayed in the primary menu.
    /// </summary>
    /// <remarks>
    /// The collection instance is owned by the control and remains stable for its lifetime. It is
    /// deliberately not registered as a dependency property: design tools otherwise interpret
    /// explicit <c>NavigationView.MenuItems</c> syntax as an attempt to set a read-only dependency
    /// property. The CLR-only collection contract matches <see cref="ItemsControl.Items"/> and is
    /// sufficient because the control forwards collection changes to its internal item hosts.
    /// </remarks>
    [Bindable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableCollection<object> MenuItems { get; } = [];

    private void OnFooterMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildNavigationIndexes();
    }

    private void OnMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildNavigationIndexes();
    }

    #endregion

    #region DP: MenuItemsSource

    /// <summary>Identifies the <see cref="MenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsSourceProperty = DependencyProperty.Register(
        nameof(MenuItemsSource),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnMenuItemsSourceChanged)
    );

    /// <summary>Gets or sets the source used to generate primary menu items.</summary>
    [Bindable(true)]
    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    private static void OnMenuItemsSourceChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.UpdateMenuItemsHosts();
        navigationView.RebuildNavigationIndexes();
    }

    #endregion

    #region DP: NavigationCacheSize

    /// <summary>Identifies the <see cref="NavigationCacheSize"/> dependency property.</summary>
    public static readonly DependencyProperty NavigationCacheSizeProperty = DependencyProperty.Register(
        nameof(NavigationCacheSize),
        typeof(int),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(10, OnNavigationCacheSizeChanged),
        static value => value is >= 0
    );

    /// <summary>
    /// Gets or sets the number of pages with <see cref="NavigationCacheMode.Evictable"/> kept in cache.
    /// Pages with <see cref="NavigationCacheMode.Permanent"/> do not count toward this size.
    /// <para>The default value is <c>10</c>.</para>
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public int NavigationCacheSize
    {
        get => (int)GetValue(NavigationCacheSizeProperty);
        set => SetValue(NavigationCacheSizeProperty, value);
    }

    private static void OnNavigationCacheSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView._cache.Trim((int)e.NewValue);
        }
    }

    #endregion

    #region DP: LeftPaneOpenWidth

    /// <summary>
    /// Identifies the <see cref="LeftPaneOpenWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftPaneOpenWidthProperty = DependencyProperty.Register(
        nameof(LeftPaneOpenWidth),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D, OnLeftPaneTargetWidthChanged)
    );

    /// <summary>
    /// Gets or sets the width used by the built-in Left pane while <see cref="IsPaneOpen"/> is
    /// <see langword="true"/>. This property is intentionally scoped to the Left template; the
    /// other display modes size their panes from their own content and layout contracts.
    /// </summary>
    public double LeftPaneOpenWidth
    {
        get => (double)GetValue(LeftPaneOpenWidthProperty);
        set => SetValue(LeftPaneOpenWidthProperty, value);
    }

    #endregion

    #region Read-only DP: LeftPaneWidth

    private static readonly DependencyPropertyKey LeftPaneWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(LeftPaneWidth),
            typeof(double),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(0D)
        );

    /// <summary>Identifies the <see cref="LeftPaneWidth"/> dependency property.</summary>
    public static readonly DependencyProperty LeftPaneWidthProperty =
        LeftPaneWidthPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the width retained for the built-in Left pane.
    /// </summary>
    /// <remarks>
    /// The value is initialized from <see cref="LeftPaneOpenWidth"/> or
    /// <see cref="LeftPaneCompactWidth"/> on the first Left template application and captures the
    /// pane's effective width before that template is replaced. Binding the Left template back to
    /// this retained value lets a later mode switch restore the pane without replaying its
    /// open/close animation.
    /// </remarks>
    public double LeftPaneWidth => (double)GetValue(LeftPaneWidthProperty);

    private static void OnLeftPaneTargetWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.UpdateVisibleLeftPaneTargetWidth(e.Property);
        }
    }

    #endregion

    #region DP: SelectedItem

    private static readonly DependencyPropertyKey SelectedItemPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SelectedItem),
            typeof(object),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="SelectedItem"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedItemProperty =
        SelectedItemPropertyKey.DependencyProperty;

    /// <summary>Gets the selected source item.</summary>
    /// <remarks>
    /// In data-driven mode this value is the object from <see cref="MenuItemsSource"/> or
    /// <see cref="FooterMenuItemsSource"/>. The generated <see cref="NavigationViewItem"/> is only
    /// presentation state and is intentionally not exposed as selection identity.
    /// </remarks>
    [Bindable(true)]
    [Browsable(false)]
    public object? SelectedItem => GetValue(SelectedItemProperty);

    private void SetSelectedItem(object? item) => SetValue(SelectedItemPropertyKey, item);

    #endregion

    #region DP: PaneDisplayMode

    /// <summary>Identifies the <see cref="PaneDisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty PaneDisplayModeProperty = DependencyProperty.Register(
        nameof(PaneDisplayMode),
        typeof(NavigationViewPaneDisplayMode),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewPaneDisplayMode.Left, OnPaneDisplayModeChanged)
    );

    /// <summary>Gets or sets how the pane and content regions are arranged.</summary>
    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    private static void OnPaneDisplayModeChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.SetValue(PaneDisplayModeContextProperty, (NavigationViewPaneDisplayMode)e.NewValue);

        // Close ContextMenu-owned state before the style trigger clears the outgoing template subtree.
        // IsExpanded is intentionally left untouched so an inline Left hierarchy survives a
        // presentation round trip.
        navigationView.CloseAllNavigationViewItemSubmenus();
        navigationView.PrepareForPaneDisplayModeChange((NavigationViewPaneDisplayMode)e.OldValue);
        navigationView.OnPaneDisplayModeChanged();
    }

    #endregion

    #region DP: PaneFooter

    /// <summary>Identifies the <see cref="PaneFooter"/> dependency property.</summary>
    public static readonly DependencyProperty PaneFooterProperty = DependencyProperty.Register(
        nameof(PaneFooter),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets content displayed at the bottom of the navigation pane.</summary>
    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    #endregion

    #region DP: PaneHeader

    /// <summary>Identifies the <see cref="PaneHeader"/> dependency property.</summary>
    public static readonly DependencyProperty PaneHeaderProperty = DependencyProperty.Register(
        nameof(PaneHeader),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets content displayed at the top of the navigation pane.</summary>
    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    #endregion

    #region DP: PaneTitle

    /// <summary>Identifies the <see cref="PaneTitle"/> dependency property.</summary>
    public static readonly DependencyProperty PaneTitleProperty = DependencyProperty.Register(
        nameof(PaneTitle),
        typeof(string),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnPaneTitleChanged)
    );

    /// <summary>Gets or sets the label displayed beside the menu icon while the pane is open.</summary>
    public string? PaneTitle
    {
        get => (string?)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    private static void OnPaneTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.PaneTitleChangedInternal?.Invoke(navigationView, EventArgs.Empty);
        }
    }

    #endregion

    #region DP: TitleBar

    /// <summary>
    /// Identifies the <see cref="TitleBar"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleBarProperty = DependencyProperty.Register(
        nameof(TitleBar),
        typeof(TitleBar),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnTitleBarChanged)
    );

    /// <summary>
    /// Gets or sets the title bar whose <see cref="Wpf.Zen.Ui.Controls.TitleBar.LeftContent"/> slot hosts
    /// the highest-priority visible navigation command.
    /// </summary>
    /// <remarks>
    /// Assigning a title bar grants this navigation view temporary ownership of that slot.
    /// The prior value is restored when the association is removed, provided the application
    /// has not replaced the contributed content in the meantime. No margins or other layout
    /// properties on either control are modified.
    /// </remarks>
    public TitleBar? TitleBar
    {
        get => (TitleBar?)GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    private static void OnTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        // TitleBar exposes a content contract, so coordination no longer requires either
        // control to know or mutate the other's margins or template geometry.
        navigationView.UpdatePaneButtonPlacement();
    }

    #endregion

    #region DP: Transition

    /// <summary>Identifies the <see cref="Transition"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition),
        typeof(Transition),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(Transition.FadeInWithSlide)
    );

    /// <summary>Gets or sets the transition used when changing pages.</summary>
    public Transition Transition
    {
        get => (Transition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    #endregion

    #region DP: TransitionDuration

    /// <summary>Identifies the <see cref="TransitionDuration"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration),
        typeof(int),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(200)
    );

    /// <summary>Gets or sets the page-transition duration, in milliseconds.</summary>
    [Bindable(true)]
    [Category("Appearance")]
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    #endregion
}
