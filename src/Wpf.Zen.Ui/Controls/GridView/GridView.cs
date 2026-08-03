// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects the Fluent <see cref="ListView"/> and item-container styles for WPF's column view.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="System.Windows.Controls.GridView"/> is a
/// <see cref="System.Windows.Controls.ViewBase"/>, not a visual
/// element, so it cannot receive a WPF <see cref="Style"/> itself. When a view is assigned,
/// <see cref="System.Windows.Controls.ListView"/> reads the view's protected style keys and applies
/// them to the list and its generated containers. This type exists only to select the library's
/// Fluent resources through that framework-supported extension point; all visuals remain in XAML.
/// </para>
/// <para>
/// The routed styles are published through <see cref="StyleKeys"/> instead of shadowing WPF's
/// native GridView resource-key properties. This keeps theme routing separate from the public,
/// strongly typed resource surface used by <c>StaticStyle</c> and <c>DynamicStyle</c>.
/// </para>
/// <para>
/// Use <see cref="GridViewColumn"/> when a column needs <see cref="GridLength"/> sizing. Native
/// <see cref="System.Windows.Controls.GridViewColumn"/> instances remain supported for WPF's
/// built-in Auto and pixel widths.
/// </para>
/// </remarks>
/// <example>
/// To use this enhanced GridView in a ListView:
/// <code lang="xml">
/// &lt;ListView&gt;
///     &lt;ListView.View&gt;
///         &lt;ui:GridView&gt;
///             &lt;ui:GridViewColumn Width="Auto" Header="First Name" DisplayMemberBinding="{Binding FirstName}"/&gt;
///             &lt;ui:GridViewColumn Width="*" Header="Last Name" DisplayMemberBinding="{Binding LastName}"/&gt;
///         &lt;/ui:GridView&gt;
///     &lt;/ListView.View&gt;
/// &lt;/ListView&gt;
/// </code>
/// </example>
public class GridView : System.Windows.Controls.GridView
{
    /// <inheritdoc />
    protected override object DefaultStyleKey => StyleKeys.GridViewListViewStyle;

    /// <inheritdoc />
    protected override object ItemContainerDefaultStyleKey => StyleKeys.GridViewItemContainerStyle;
}
