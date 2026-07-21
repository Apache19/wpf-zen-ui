// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

/// <summary>
/// Displays <see cref="ListView"/> data in columns with Fluent styling and Grid-like column sizing.
/// </summary>
/// <example>
/// To use this enhanced GridView in a ListView:
/// <code lang="xml">
/// &lt;ListView&gt;
///     &lt;ListView.View&gt;
///         &lt;local:GridView&gt;
///             &lt;GridViewColumn Header="First Name" DisplayMemberBinding="{Binding FirstName}"/&gt;
///             &lt;GridViewColumn Header="Last Name" DisplayMemberBinding="{Binding LastName}"/&gt;
///         &lt;/local:GridView&gt;
///     &lt;/ListView.View&gt;
/// &lt;/ListView&gt;
/// </code>
/// </example>
public class GridView : System.Windows.Controls.GridView
{
    /// <summary>
    /// Gets the resource key for the default <see cref="ListView"/> style used by this view.
    /// </summary>
    public static new ResourceKey GridViewStyleKey { get; } =
        new ComponentResourceKey(typeof(GridView), nameof(GridViewStyleKey));

    /// <summary>
    /// Gets the resource key for the default item container style used by this view.
    /// </summary>
    public static new ResourceKey GridViewItemContainerStyleKey { get; } =
        new ComponentResourceKey(typeof(GridView), nameof(GridViewItemContainerStyleKey));

    /// <inheritdoc />
    protected override object DefaultStyleKey => GridViewStyleKey;

    /// <inheritdoc />
    protected override object ItemContainerDefaultStyleKey => GridViewItemContainerStyleKey;
}
