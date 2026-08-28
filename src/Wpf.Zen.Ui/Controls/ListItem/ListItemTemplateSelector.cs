// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects a <see cref="DataTemplate"/> for an item according to its position in an owning
/// <see cref="ItemsControl"/>.
/// </summary>
/// <remarks>
/// <para>
/// Position-specific templates are evaluated from the most specific rule to the least specific
/// rule: <see cref="SingleItemTemplate"/>, <see cref="FirstItemTemplate"/>,
/// <see cref="LastItemTemplate"/>, the applicable alternating template, and finally
/// <see cref="DefaultTemplate"/>.
/// </para>
/// <para>
/// The selector resolves the generated item container instead of searching for the data item by
/// value. Equal or repeated items are therefore classified by their actual collection positions.
/// </para>
/// <para>
/// WPF does not ordinarily re-run a template selector for surviving containers after an insertion,
/// removal, or move. This selector observes each owning item-container generator and invalidates
/// only realized containers, keeping all positional rules current without defeating virtualization.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ItemsControl.ItemTemplateSelector&gt;
///     &lt;ui:ListItemTemplateSelector
///         DefaultTemplate="{StaticResource ItemTemplate}"
///         FirstItemTemplate="{StaticResource FirstItemTemplate}"
///         LastItemTemplate="{StaticResource LastItemTemplate}"
///         OddItemTemplate="{StaticResource OddItemTemplate}" /&gt;
/// &lt;/ItemsControl.ItemTemplateSelector&gt;
/// </code>
/// </example>
public sealed class ListItemTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Gets or sets the fallback template used when no more specific positional rule supplies a
    /// template. The default value is <see langword="null"/>, which lets WPF continue with its
    /// ordinary implicit-template lookup.
    /// </summary>
    public DataTemplate? DefaultTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for the first item. <see cref="SingleItemTemplate"/> takes
    /// precedence when there is only one item. If this value is <see langword="null"/>, selection
    /// continues with the remaining rules.
    /// </summary>
    public DataTemplate? FirstItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for the last item. For a single-item collection this rule is
    /// considered after <see cref="SingleItemTemplate"/> and <see cref="FirstItemTemplate"/>. If
    /// this value is <see langword="null"/>, selection continues with the remaining rules.
    /// </summary>
    public DataTemplate? LastItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template used when the collection contains exactly one item. This rule
    /// avoids an arbitrary choice between <see cref="FirstItemTemplate"/> and
    /// <see cref="LastItemTemplate"/>. If it is not set, the first-item rule is considered next.
    /// </summary>
    public DataTemplate? SingleItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for items whose zero-based index is even. The first item therefore
    /// participates in this rule when no first- or single-item template is supplied.
    /// </summary>
    public DataTemplate? EvenItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for items whose zero-based index is odd.
    /// </summary>
    public DataTemplate? OddItemTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (
            !ListItemTemplateManager.TryFindOwner(
                container,
                out ItemsControl owner,
                out DependencyObject itemContainer
            )
        )
        {
            return DefaultTemplate;
        }

        ListItemTemplateManager.RegisterOwner(owner, this);

        int index = owner.ItemContainerGenerator.IndexFromContainer(itemContainer);
        if (index < 0)
        {
            // Generator association normally exists before ContentPresenter asks for a template.
            // The value lookup is only a fallback for custom container implementations; equal
            // duplicate values cannot be distinguished until such a container is registered.
            index = owner.Items.IndexOf(item);
        }

        return SelectTemplate(index, owner.Items.Count);
    }

    /// <summary>
    /// Applies positional precedence without depending on container realization. Keeping this
    /// decision separate also gives the refresh manager exactly the same rules as initial selection.
    /// </summary>
    internal DataTemplate? SelectTemplate(int index, int itemCount)
    {
        if (index < 0 || index >= itemCount)
        {
            return DefaultTemplate;
        }

        if (itemCount == 1 && SingleItemTemplate is not null)
        {
            return SingleItemTemplate;
        }

        if (index == 0 && FirstItemTemplate is not null)
        {
            return FirstItemTemplate;
        }

        if (index == itemCount - 1 && LastItemTemplate is not null)
        {
            return LastItemTemplate;
        }

        DataTemplate? alternatingTemplate = index % 2 == 0 ? EvenItemTemplate : OddItemTemplate;
        return alternatingTemplate ?? DefaultTemplate;
    }
}
