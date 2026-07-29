// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls;
using Wpf.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines the dependency properties and dp callbacks for <see cref="NavigationView"/> control
/// </content>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public partial class NavigationView
{
    #region Read-only attached DP: ContentPadding

    private static readonly DependencyPropertyKey ContentPaddingPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "ContentPadding",
            typeof(Thickness),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>
    /// Identifies the read-only <c>NavigationView.ContentPadding</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// <see cref="NavigationView"/> publishes its <see cref="Control.Padding"/> value on the root
    /// object currently hosted by its private <see cref="Frame"/>. The value then inherits through
    /// that root's page subtree, allowing a page-owned scroll viewer to use it as content padding
    /// while its own viewport remains edge-to-edge. It is read-only because the value describes
    /// navigation-host state and must not diverge from the owning control.
    /// </remarks>
    public static readonly DependencyProperty ContentPaddingProperty =
        ContentPaddingPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the content inset published by the <see cref="NavigationView"/> currently hosting
    /// <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// The navigated root element, or one of its descendants, from which to read the
    /// host-provided inset.
    /// </param>
    /// <returns>
    /// The owning navigation view's <see cref="Control.Padding"/>, or an empty
    /// <see cref="Thickness"/> when the element is not current navigation content.
    /// </returns>
    /// <remarks>
    /// WPF's <see cref="Frame"/> uses <see cref="InheritanceBehavior.SkipToAppNow"/>, so a
    /// navigated <see cref="Page"/> cannot reliably locate the frame with an ancestor binding or
    /// inherit a property from it. This attached value is the explicit bridge for layout metadata;
    /// it does not add scrolling and does not modify any page-owned layout property. The value
    /// participates in property-value inheritance only after it has been published on the current
    /// navigation root; it does not cross into a Page from the Frame.
    /// </remarks>
    public static Thickness GetContentPadding(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (Thickness)element.GetValue(ContentPaddingProperty);
    }

    private static void OnPaddingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.UpdateNavigatedContentPadding();
        }
    }

    #endregion

    #region Attached DP: IsContentPaddingHandled

    /// <summary>
    /// Identifies the <c>NavigationView.IsContentPaddingHandled</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. In that mode the private navigation frame
    /// applies <see cref="Control.Padding"/> outside the current content, which is appropriate for
    /// ordinary pages. A page template that places a scroll viewer at its outer edge should set
    /// this value to <see langword="true"/> and apply <see cref="ContentPaddingProperty"/> inside
    /// that viewer instead. This explicit contract avoids inferring layout ownership from
    /// <see cref="ScrollViewer.CanContentScrollProperty"/> or from an incompletely generated visual
    /// tree.
    /// </remarks>
    public static readonly DependencyProperty IsContentPaddingHandledProperty =
        DependencyProperty.RegisterAttached(
            "IsContentPaddingHandled",
            typeof(bool),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(false, OnIsContentPaddingHandledChanged)
        );

    private static void OnIsContentPaddingHandledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        // A DynamicResource page style can resolve after Frame.Navigated has fired. The private
        // host marker lets this late change refresh only the NavigationView that currently owns
        // the content, without walking across Frame's inheritance boundary.
        if (d.GetValue(ContentPaddingHostProperty) is NavigationView navigationView)
        {
            navigationView.UpdateNavigationFramePadding();
        }
    }

    /// <summary>
    /// Gets whether <paramref name="element"/> applies the owning navigation view's
    /// <see cref="ContentPaddingProperty"/> within its own layout.
    /// </summary>
    /// <param name="element">The navigation content root to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the content handles the inset; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool GetIsContentPaddingHandled(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(IsContentPaddingHandledProperty);
    }

    /// <summary>
    /// Sets whether <paramref name="element"/> applies the owning navigation view's
    /// <see cref="ContentPaddingProperty"/> within its own layout.
    /// </summary>
    /// <param name="element">The navigation content root on which to declare layout ownership.</param>
    /// <param name="value">
    /// <see langword="true"/> to keep the content edge-to-edge and consume
    /// <see cref="ContentPaddingProperty"/> inside it; <see langword="false"/> to let the frame
    /// apply the inset outside the content.
    /// </param>
    public static void SetIsContentPaddingHandled(DependencyObject element, bool value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IsContentPaddingHandledProperty, value);
    }

    #endregion

    #region DP: AlwaysShowHeader

    /// <summary>Identifies the <see cref="AlwaysShowHeader"/> dependency property.</summary>
    public static readonly DependencyProperty AlwaysShowHeaderProperty = DependencyProperty.Register(
        nameof(AlwaysShowHeader),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

        if (e.NewValue is null && e.OldValue is AutoSuggestBox oldValue)
        {
            oldValue.SuggestionChosen -= navigationView.AutoSuggestBoxOnSuggestionChosen;
            oldValue.QuerySubmitted -= navigationView.AutoSuggestBoxOnQuerySubmitted;
            return;
        }

        if (e.NewValue is not AutoSuggestBox autoSuggestBox)
        {
            return;
        }

        autoSuggestBox.OriginalItemsSource = navigationView._autoSuggestItems;
        navigationView.UpdateAutoSuggestBoxSuggestions();

        autoSuggestBox.SuggestionChosen += navigationView.AutoSuggestBoxOnSuggestionChosen;
        autoSuggestBox.QuerySubmitted += navigationView.AutoSuggestBoxOnQuerySubmitted;
    }

    #endregion

    #region DP: BreadcrumbBar

    /// <summary>Identifies the <see cref="BreadcrumbBar"/> dependency property.</summary>
    public static readonly DependencyProperty BreadcrumbBarProperty = DependencyProperty.Register(
        nameof(BreadcrumbBar),
        typeof(BreadcrumbBar),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnBreadcrumbBarChanged)
    );

    /// <inheritdoc/>
    public BreadcrumbBar? BreadcrumbBar
    {
        get => (BreadcrumbBar?)GetValue(BreadcrumbBarProperty);
        set => SetValue(BreadcrumbBarProperty, value);
    }

    private static void OnBreadcrumbBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if (e.OldValue is BreadcrumbBar oldValue)
        {
            navigationView.DetachBreadcrumbBar(oldValue);

            if (ReferenceEquals(oldValue.ItemsSource, navigationView.BreadcrumbItems))
            {
                // SetCurrentValue mirrors the assignment below: a consumer binding must survive
                // detachment even when it happened to evaluate to our collection at this moment.
                oldValue.SetCurrentValue(ItemsControl.ItemsSourceProperty, null);
            }
        }

        if (e.NewValue is not BreadcrumbBar breadcrumbBar)
        {
            return;
        }

        // The collaboration owns only data and navigation. BreadcrumbBar retains complete control
        // over ItemTemplate, ItemTemplateSelector, and ItemContainerStyle so the same source item
        // can have a presentation distinct from the navigation pane.
        breadcrumbBar.SetCurrentValue(ItemsControl.ItemsSourceProperty, navigationView.BreadcrumbItems);
        navigationView.AttachBreadcrumbBar(breadcrumbBar);
    }

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

    #region DP: ContentOverlay

    /// <summary>Identifies the <see cref="ContentOverlay"/> dependency property.</summary>
    public static readonly DependencyProperty ContentOverlayProperty = DependencyProperty.Register(
        nameof(ContentOverlay),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public object? ContentOverlay
    {
        get => GetValue(ContentOverlayProperty);
        set => SetValue(ContentOverlayProperty, value);
    }

    #endregion

    #region DP: EnableDebugMessages

    /// <summary>Identifies the <see cref="EnableDebugMessages"/> dependency property.</summary>
    public static readonly DependencyProperty EnableDebugMessagesProperty = DependencyProperty.Register(
        nameof(EnableDebugMessages),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether debugging messages for this control are enabled
    /// </summary>
    public bool EnableDebugMessages
    {
        get => (bool)GetValue(EnableDebugMessagesProperty);
        set => SetValue(EnableDebugMessagesProperty, value);
    }

    #endregion

    #region DP: FooterMenuItems

    private static readonly DependencyPropertyKey FooterMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FooterMenuItems),
            typeof(ObservableCollection<object>),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="FooterMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemsProperty =
        FooterMenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList FooterMenuItems => (ObservableCollection<object>)GetValue(FooterMenuItemsProperty);

    #endregion

    #region DP: FooterMenuItemsSource

    /// <summary>Identifies the <see cref="FooterMenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemsSourceProperty = DependencyProperty.Register(
        nameof(FooterMenuItemsSource),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnFooterMenuItemsSourceChanged)
    );

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>Identifies the read-only <see cref="HasFooterMenuItems"/> dependency property.</summary>
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

    /// <summary>Identifies the read-only <see cref="HasMenuItems"/> dependency property.</summary>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
        ValidateSuppressedNavigationInput
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

    private static bool ValidateSuppressedNavigationInput(object? value) =>
        value is NavigationViewInput input && (input & ~NavigationViewInput.All) == 0;

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

    /// <summary>
    /// Identifies the read-only <see cref="IsBackEnabled"/> dependency property.
    /// </summary>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool IsTopSeparatorVisible
    {
        get => (bool)GetValue(IsTopSeparatorVisibleProperty);
        set => SetValue(IsTopSeparatorVisibleProperty, value);
    }

    #endregion

    #region DP: ItemContainerStyle

    /// <summary>Identifies the <see cref="ItemContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerStyleProperty =
        DependencyProperty.Register(
            nameof(ItemContainerStyle),
            typeof(Style),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <inheritdoc/>
    public Style? ItemContainerStyle
    {
        get => (Style?)GetValue(ItemContainerStyleProperty);
        set => SetValue(ItemContainerStyleProperty, value);
    }

    #endregion

    #region DP: ItemContainerStyleSelector

    /// <summary>Identifies the <see cref="ItemContainerStyleSelector"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerStyleSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemContainerStyleSelector),
            typeof(StyleSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <inheritdoc/>
    public StyleSelector? ItemContainerStyleSelector
    {
        get => (StyleSelector?)GetValue(ItemContainerStyleSelectorProperty);
        set => SetValue(ItemContainerStyleSelectorProperty, value);
    }

    #endregion

    #region DP: ItemTemplate

    /// <summary>Identifies the <see cref="ItemTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <inheritdoc/>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    #endregion

    #region DP: ItemTemplateSelector

    /// <summary>Identifies the <see cref="ItemTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <inheritdoc/>
    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    #endregion

    #region DP: MenuItems

    private static readonly DependencyPropertyKey MenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MenuItems),
        typeof(ObservableCollection<object>),
        typeof(NavigationView),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="MenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

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

    /// <inheritdoc/>
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
    /// Gets or sets the number of pages with <see cref="NavigationCacheMode.Enabled"/> kept in cache.
    /// Pages with <see cref="NavigationCacheMode.Required"/> do not count toward this size.
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

    /// <summary>
    /// Identifies the read-only <see cref="LeftPaneWidth"/> dependency property.
    /// </summary>
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

    /// <summary>Identifies the read-only <see cref="SelectedItem"/> dependency property.</summary>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
    /// Gets or sets the title bar whose <see cref="Wpf.Ui.Controls.TitleBar.LeftContent"/> slot hosts
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    [Bindable(true)]
    [Category("Appearance")]
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    #endregion
}
