// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using UiListViewItem = Wpf.Ui.Controls.ListViewItem;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ListView"/> with Fluent item containers and selection visuals.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:ListView ItemsSource="{Binding ...}" &gt;
///     &lt;ui:ListView.View&gt;
///         &lt;ui:GridView&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding FirstName}"
///                 Header="First Name" /&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding LastName}"
///                 Header="Last Name" /&gt;
///         &lt;/ui:GridView&gt;
///     &lt;/ui:ListView.View&gt;
/// &lt;/ui:ListView&gt;
/// </code>
/// </example>
public class ListView : System.Windows.Controls.ListView
{
    /// <summary>
    /// Identifies the <see cref="IsSelectionIndicatorVisible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectionIndicatorVisibleProperty =
        SelectionIndicator.IsSelectionIndicatorVisibleProperty.AddOwner(
            typeof(ListView),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    static ListView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ListView),
            new FrameworkPropertyMetadata(typeof(ListView))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selected item displays an accent-colored indicator at its leading edge.
    /// </summary>
    public bool IsSelectionIndicatorVisible
    {
        get => (bool)GetValue(IsSelectionIndicatorVisibleProperty);
        set => SetValue(IsSelectionIndicatorVisibleProperty, value);
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new UiListViewItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is UiListViewItem;
    }
}
