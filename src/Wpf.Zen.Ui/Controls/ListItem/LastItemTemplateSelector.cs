// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects a distinct <see cref="DataTemplate"/> for the last item in any
/// <see cref="ItemsControl"/>.
/// </summary>
/// <remarks>
/// <para>
/// The selector uses the owning control's item-container generator to determine position. It
/// therefore remains correct when a collection contains equal or repeated data items.
/// </para>
/// <para>
/// WPF chooses an item template when a container is prepared. If a collection is mutated in place,
/// WPF does not necessarily re-evaluate the templates of surviving containers whose positions have
/// changed. Replace the items source or refresh its view when those containers must be reclassified.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ItemsControl.ItemTemplateSelector&gt;
///     &lt;ui:LastItemTemplateSelector
///         NormalTemplate="{StaticResource ItemTemplate}"
///         LastItemTemplate="{StaticResource LastItemTemplate}" /&gt;
/// &lt;/ItemsControl.ItemTemplateSelector&gt;
/// </code>
/// </example>
public sealed class LastItemTemplateSelector : DataTemplateSelector
{
    // ┌─────────────────────────────────────────┐
    // │ Product List                            │
    // │┌───────────────────────────────────────┐│
    // ││ Product A                             ││ ← NormalTemplate
    // │├───────────────────────────────────────┤│
    // ││ Product B                             ││ ← NormalTemplate
    // │├───────────────────────────────────────┤│
    // ││ Product C                             ││ ← NormalTemplate
    // │├───────────────────────────────────────┤│
    // ││ [Load More...]                        ││ ← LastItemTemplate（different template for the last item）
    // │└───────────────────────────────────────┘│
    // └─────────────────────────────────────────┘

    /// <summary>
    /// Gets or sets the template used for every item except the last item. The default value is
    /// <see langword="null"/>, which lets WPF continue with its ordinary implicit-template lookup.
    /// </summary>
    public DataTemplate? NormalTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template used for the last item. The default value is <see langword="null"/>.
    /// When no last-item template is supplied, <see cref="NormalTemplate"/> is used instead.
    /// </summary>
    public DataTemplate? LastItemTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is null)
        {
            return null;
        }

        if (ItemsControl.ItemsControlFromItemContainer(container) is not { } owner)
        {
            return NormalTemplate;
        }

        int index = owner.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            // WPF associates its generated ContentPresenter before template selection. Retain a
            // data-item fallback for custom generators, accepting that equal duplicate values cannot
            // be distinguished until their containers are registered.
            index = owner.Items.IndexOf(item);
        }

        return index >= 0 && index == owner.Items.Count - 1
            ? LastItemTemplate ?? NormalTemplate
            : NormalTemplate;
    }
}
