// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Hosts the items displayed by a <see cref="NavigationView"/> or <see cref="NavigationViewItem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a general-purpose <see cref="ItemsControl"/>, this control understands that a navigation
/// menu can contain structural <see cref="UIElement"/> values alongside navigation data. Existing
/// elements remain their own containers, while non-visual data receives a
/// <see cref="NavigationViewItem"/> container. This makes the list the root container-generation
/// boundary and matches the child generation performed by <see cref="NavigationViewItem"/>.
/// </para>
/// <para>
/// The complete list contributes one stop to the surrounding Tab sequence. Tab enters its first
/// or most recently active item header and the following Tab leaves the list; arrow keys move focus
/// among the item headers without escaping the list. Applications can replace either standard WPF
/// keyboard-navigation mode when a specialized host requires different behavior.
/// </para>
/// </remarks>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public class NavigationViewList : ItemsControl
{
    /// <summary>
    /// Identifies the <see cref="IsDynamicScrollViewerEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsDynamicScrollViewerEnabledProperty =
        DependencyProperty.Register(
            nameof(IsDynamicScrollViewerEnabled),
            typeof(bool),
            typeof(NavigationViewList),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    static NavigationViewList()
    {
        // NavigationViewList owns a scrolling control template rather than inheriting the plain
        // ItemsControl template. A type-specific default-style key is therefore required for WPF's
        // theme lookup to find NavigationViewList.xaml even when the application sets no Style.
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewList),
            new FrameworkPropertyMetadata(typeof(NavigationViewList))
        );

        // A navigation menu is a single composite control in the window's Tab sequence. Once lets
        // WPF enter through the first header on the initial visit (or the active header on a later
        // visit) and then leave the complete list on the next Tab or Shift+Tab. Contained reserves
        // the arrow keys for moving among item headers. These are metadata defaults, matching
        // ListBox, so an application can still replace either policy for a specialized host.
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(
            typeof(NavigationViewList),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Once)
        );
        KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(
            typeof(NavigationViewList),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained)
        );
    }

    /// <summary>
    /// Gets a value indicating whether this control, rather than the scrolling host in its template,
    /// owns keyboard scrolling and directional focus navigation.
    /// </summary>
    /// <remarks>
    /// WPF's <see cref="ScrollViewer"/> otherwise handles arrow keys before the routed event reaches
    /// the templated parent. Returning <see langword="true"/> is the same contract used by
    /// <see cref="ListBox"/> and allows <see cref="OnKeyDown(KeyEventArgs)"/> to apply one consistent
    /// focus model for either built-in scrolling template.
    /// </remarks>
    protected override bool HandlesScrolling => true;

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="KeyboardNavigation.DirectionalNavigationProperty"/> describes the boundary used
    /// by WPF's focus search, but an <see cref="ItemsControl"/> does not translate arrow-key input
    /// into that search. The focused header button raises the key event through its owning
    /// <see cref="NavigationViewItem"/> first, so hierarchy-specific Left/Right behavior retains
    /// priority. Any unhandled arrow then reaches this list and moves spatially to the next visible,
    /// enabled focus target. The <see cref="KeyboardNavigationMode.Contained"/> metadata above keeps
    /// an edge arrow within the composite list.
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (
            TryGetFocusNavigationDirection(e.Key, out FocusNavigationDirection direction)
            && Keyboard.FocusedElement is UIElement focusedElement
            && IsKeyboardFocusWithin
        )
        {
            _ = focusedElement.MoveFocus(new TraversalRequest(direction));

            // Direction keys belong to the composite even at an edge, where Contained correctly
            // leaves focus on the current header. Letting an unsuccessful edge traversal bubble
            // would allow an outer ScrollViewer or command binding to reinterpret the same key.
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Maps the four spatial arrow keys to WPF focus-navigation directions.</summary>
    private static bool TryGetFocusNavigationDirection(
        Key key,
        out FocusNavigationDirection direction)
    {
        direction = key switch
        {
            Key.Left => FocusNavigationDirection.Left,
            Key.Up => FocusNavigationDirection.Up,
            Key.Right => FocusNavigationDirection.Right,
            Key.Down => FocusNavigationDirection.Down,
            _ => default,
        };

        return key is Key.Left or Key.Up or Key.Right or Key.Down;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the list template uses a
    /// <see cref="DynamicScrollViewer"/> instead of a <see cref="UiScrollViewer"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. This property changes only the scrolling host used
    /// by the control template; the standard <see cref="ScrollViewer"/> attached properties continue
    /// to configure content scrolling and scroll-bar visibility in either mode.
    /// </remarks>
    public bool IsDynamicScrollViewerEnabled
    {
        get => (bool)GetValue(IsDynamicScrollViewerEnabledProperty);
        set => SetValue(IsDynamicScrollViewerEnabledProperty, value);
    }

    /// <summary>
    /// Determines whether this list should apply its item-container style or style selector to the
    /// specified item.
    /// </summary>
    /// <param name="container">The element used to display <paramref name="item"/>.</param>
    /// <param name="item">The item being prepared for display.</param>
    /// <returns>
    /// The result of the base WPF implementation when <paramref name="container"/> is a
    /// <see cref="NavigationViewItem"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// WPF validates an item-container style's target type before assigning it. Structural elements
    /// remain their own containers and can therefore be incompatible with a navigation-item style;
    /// generated and directly supplied <see cref="NavigationViewItem"/> containers pass this check.
    /// </remarks>
    protected override bool ShouldApplyItemContainerStyle(DependencyObject container, object item)
    {
        return container is NavigationViewItem
            && base.ShouldApplyItemContainerStyle(container, item);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        // Structural elements supplied by the application, such as a Separator or a group label,
        // must remain intact. Plain data is the only category for which this list owns
        // navigation-container generation.
        return item is UIElement;
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new NavigationViewItem();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Registration happens after the base call because HeaderedItemsControl preparation copies the
    /// data item into Header and applies ItemContainerStyle bindings. Lookup keys and page types are
    /// therefore observed in their prepared form rather than from an incomplete container.
    /// </remarks>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is NavigationViewItem navigationItem)
        {
            if (ReferenceEquals(element, item))
            {
                navigationItem.InheritItemContainerPresentationFrom(this);
            }

            PrepareDataContainer(navigationItem, item);

            if (NavigationView.GetNavigationParent(this) is { } navigationView)
            {
                navigationItem.AttachToNavigationViewHost(navigationView);

                if (!ReferenceEquals(element, item))
                {
                    navigationView.RegisterGeneratedNavigationItem(navigationItem, item);
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationViewItem navigationItem)
        {
            if (ReferenceEquals(element, item))
            {
                navigationItem.ClearInheritedItemContainerPresentation(this);
            }

            if (NavigationView.GetNavigationParent(this) is { } navigationView)
            {
                if (!ReferenceEquals(element, item))
                {
                    navigationView.UnregisterGeneratedNavigationItem(navigationItem);
                    navigationItem.DetachFromNavigationViewHost(navigationView);
                }
                else if (!navigationView.ContainsNavigationItemSource(navigationItem))
                {
                    // Direct containers remain semantically attached while a template merely
                    // replaces its list presenter. Once the source itself is removed, however, the
                    // local host value and source bindings must be released just like a generated
                    // container's state.
                    navigationItem.DetachFromNavigationViewHost(navigationView);
                }
            }

            if (!ReferenceEquals(element, item))
            {
                ClearDataContainer(navigationItem);
            }
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <summary>
    /// Applies the optional convenience presentation supplied by the library navigation model.
    /// </summary>
    /// <remarks>
    /// Navigation semantics are read from the source entry, not these container values. Children
    /// and icons are projected only so <see cref="NavigationItem"/> remains useful without a custom
    /// style; an explicit hierarchical template or container style still wins through dependency-
    /// property precedence.
    /// </remarks>
    internal static void PrepareDataContainer(NavigationViewItem container, object item)
    {
        if (item is not INavigationItem dataItem)
        {
            return;
        }

        if (container.ItemsSource is null && dataItem.Children is not null)
        {
            container.SetCurrentValue(ItemsControl.ItemsSourceProperty, dataItem.Children);
            container.UsesNavigationDataChildren = true;
        }

        if (container.Icon is null && dataItem.Icon is not null)
        {
            // Keep the source value as data. Each ContentPresenter can then apply an implicit or
            // explicit DataTemplate and create its own visual instance through normal WPF rules.
            container.SetCurrentValue(NavigationViewItem.IconProperty, dataItem.Icon);
            container.UsesNavigationDataIcon = true;
        }
    }

    /// <summary>
    /// Synchronizes values previously projected by <see cref="PrepareDataContainer"/> after a data
    /// item reports a property change.
    /// </summary>
    /// <remarks>
    /// Ownership flags are essential here. An ItemsSource or Icon supplied by an application style
    /// must continue to win; only values created by the default convenience projection are replaced.
    /// </remarks>
    internal static void SynchronizeDataContainer(NavigationViewItem container, INavigationItem item)
    {
        if (container.UsesNavigationDataChildren)
        {
            if (item.Children is null)
            {
                container.ClearValue(ItemsControl.ItemsSourceProperty);
                container.UsesNavigationDataChildren = false;
            }
            else
            {
                container.SetCurrentValue(ItemsControl.ItemsSourceProperty, item.Children);
            }
        }
        else if (container.ItemsSource is null && item.Children is not null)
        {
            container.SetCurrentValue(ItemsControl.ItemsSourceProperty, item.Children);
            container.UsesNavigationDataChildren = true;
        }

        if (container.UsesNavigationDataIcon)
        {
            if (item.Icon is null)
            {
                container.ClearValue(NavigationViewItem.IconProperty);
                container.UsesNavigationDataIcon = false;
            }
            else
            {
                container.SetCurrentValue(NavigationViewItem.IconProperty, item.Icon);
            }
        }
        else if (container.Icon is null && item.Icon is not null)
        {
            container.SetCurrentValue(NavigationViewItem.IconProperty, item.Icon);
            container.UsesNavigationDataIcon = true;
        }
    }

    /// <summary>
    /// Releases only values owned by the default data projection before WPF reuses a generated
    /// container for another source item.
    /// </summary>
    internal static void ClearDataContainer(NavigationViewItem container)
    {
        if (container.UsesNavigationDataChildren)
        {
            container.ClearValue(ItemsControl.ItemsSourceProperty);
            container.UsesNavigationDataChildren = false;
        }

        if (container.UsesNavigationDataIcon)
        {
            container.ClearValue(NavigationViewItem.IconProperty);
            container.UsesNavigationDataIcon = false;
        }
    }
}
