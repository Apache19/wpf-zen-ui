// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Hosts the items displayed by a <see cref="NavigationView"/> or <see cref="NavigationViewItem"/>.
/// </summary>
/// <remarks>
/// Unlike a general-purpose <see cref="ItemsControl"/>, this control understands that a navigation
/// menu can temporarily contain structural elements or other objects alongside <see cref="NavigationViewItem"/>
/// containers. Until the list owns navigation-item generation, it therefore applies navigation-item
/// container styles only to existing <see cref="NavigationViewItem"/> containers. Keeping this behavior
/// in a dedicated list also provides a single container-generation boundary for future data-driven
/// navigation improvements.
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

    /// <summary>
    /// Identifies the <see cref="ScrollSpillMode"/> dependency property.
    /// </summary>
    /// <remarks>
    /// This property is owned by both <see cref="PassiveScrollViewer"/> and
    /// <see cref="NavigationViewList"/> so a control template can forward the list-level setting to
    /// its internal scrolling host without exposing that host as part of the public visual tree.
    /// </remarks>
    public static readonly DependencyProperty ScrollSpillModeProperty =
        PassiveScrollViewer.ScrollSpillModeProperty.AddOwner(
            typeof(NavigationViewList),
            new FrameworkPropertyMetadata(ScrollSpillMode.Enabled)
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
    }

    /// <summary>
    /// Gets or sets a value indicating whether the list template uses a
    /// <see cref="DynamicScrollViewer"/> instead of a <see cref="PassiveScrollViewer"/>.
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
    /// Gets or sets how the internal <see cref="PassiveScrollViewer"/> handles mouse-wheel input
    /// when this list cannot scroll farther in the requested direction.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="Controls.ScrollSpillMode.Enabled"/>, which lets an ancestor scrolling
    /// surface continue scrolling. Popup menus can use <see cref="Controls.ScrollSpillMode.Disabled"/>
    /// to keep wheel input inside the popup even at its boundaries.
    /// </remarks>
    public ScrollSpillMode ScrollSpillMode
    {
        get => (ScrollSpillMode)GetValue(ScrollSpillModeProperty);
        set => SetValue(ScrollSpillModeProperty, value);
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
    /// WPF validates an item-container style's target type before assigning it. Because this list does not
    /// yet generate <see cref="NavigationViewItem"/> containers for arbitrary data, structural elements and
    /// other directly supplied objects can retain incompatible container types. Restricting style application
    /// by the actual container type prevents those heterogeneous items from receiving a navigation-item style.
    /// When navigation-item generation is introduced, generated <see cref="NavigationViewItem"/> containers
    /// will naturally pass the same check without changing this contract.
    /// </remarks>
    protected override bool ShouldApplyItemContainerStyle(DependencyObject container, object item)
    {
        return container is NavigationViewItem
            && base.ShouldApplyItemContainerStyle(container, item);
    }
}
