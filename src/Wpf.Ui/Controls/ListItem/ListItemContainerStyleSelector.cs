// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Selects a <see cref="Style"/> for a list item according to its position in an owning
/// <see cref="ListBox"/> or <see cref="ListView"/>.
/// </summary>
/// <remarks>
/// <para>
/// Position-specific styles are evaluated from the most specific rule to the least specific rule:
/// <see cref="SingleItemStyle"/>, <see cref="FirstItemStyle"/>, <see cref="LastItemStyle"/>, the
/// applicable alternating style, and finally <see cref="DefaultStyle"/>.
/// </para>
/// <para>
/// The WPF UI <see cref="ListBox"/> and <see cref="ListView"/> re-evaluate this selector for
/// realized containers after their collections change. Native WPF list controls still use the
/// selector during normal container preparation, but do not provide that additional refresh.
/// </para>
/// <para>
/// Use style triggers for state that can change independently of the collection, such as selection,
/// focus, pointer-over, or enabled state. A style selector is intended to classify a container when
/// it is prepared or when its collection position changes.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:ListBox.ItemContainerStyleSelector&gt;
///     &lt;ui:ListItemContainerStyleSelector
///         DefaultStyle="{StaticResource ItemStyle}"
///         LastItemStyle="{StaticResource LastItemStyle}" /&gt;
/// &lt;/ui:ListBox.ItemContainerStyleSelector&gt;
/// </code>
/// </example>
public sealed class ListItemContainerStyleSelector : StyleSelector
{
    /// <summary>
    /// Gets or sets the fallback style used when no more specific positional rule supplies a style.
    /// The default value is <see langword="null"/>, which lets WPF use the container's implicit style.
    /// </summary>
    public Style? DefaultStyle { get; set; }

    /// <summary>
    /// Gets or sets the style for the first item. <see cref="SingleItemStyle"/> takes precedence when
    /// there is only one item. If this value is <see langword="null"/>, selection continues with the
    /// remaining rules.
    /// </summary>
    public Style? FirstItemStyle { get; set; }

    /// <summary>
    /// Gets or sets the style for the last item. For a single-item collection this rule is considered
    /// after <see cref="SingleItemStyle"/> and <see cref="FirstItemStyle"/>. If this value is
    /// <see langword="null"/>, selection continues with the remaining rules.
    /// </summary>
    public Style? LastItemStyle { get; set; }

    /// <summary>
    /// Gets or sets the style used when the collection contains exactly one item. This rule avoids
    /// an arbitrary choice between <see cref="FirstItemStyle"/> and <see cref="LastItemStyle"/>.
    /// If it is not set, the first-item rule is considered next.
    /// </summary>
    public Style? SingleItemStyle { get; set; }

    /// <summary>
    /// Gets or sets the style for items whose zero-based index is even. The first item therefore
    /// participates in this rule when no first- or single-item style is supplied.
    /// </summary>
    public Style? EvenItemStyle { get; set; }

    /// <summary>
    /// Gets or sets the style for items whose zero-based index is odd.
    /// </summary>
    public Style? OddItemStyle { get; set; }

    /// <inheritdoc />
    public override Style? SelectStyle(object item, DependencyObject container)
    {
        Style? style;
        if (ItemsControl.ItemsControlFromItemContainer(container) is not ItemsControl owner)
        {
            style = DefaultStyle;
        }
        else
        {
            int index = owner.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0)
            {
                // WPF normally registers the generator association before asking for the style. Keep a
                // data-item fallback for custom generators, while accepting that equal duplicate values
                // cannot be distinguished until their containers are registered.
                index = owner.Items.IndexOf(item);
            }

            style = SelectStyle(index, owner.Items.Count);
        }

        ListItemContainerStyleManager.RecordSelection(container, this, style);
        return style;
    }

    /// <summary>
    /// Applies positional precedence without depending on container realization. WPF UI list
    /// controls use this path because WPF does not re-run a selector for surviving containers after
    /// an insertion, removal, or move.
    /// </summary>
    internal Style? SelectStyle(int index, int itemCount)
    {
        if (index < 0 || index >= itemCount)
        {
            return DefaultStyle;
        }

        if (itemCount == 1 && SingleItemStyle is not null)
        {
            return SingleItemStyle;
        }

        if (index == 0 && FirstItemStyle is not null)
        {
            return FirstItemStyle;
        }

        if (index == itemCount - 1 && LastItemStyle is not null)
        {
            return LastItemStyle;
        }

        Style? alternatingStyle = index % 2 == 0 ? EvenItemStyle : OddItemStyle;
        return alternatingStyle ?? DefaultStyle;
    }
}