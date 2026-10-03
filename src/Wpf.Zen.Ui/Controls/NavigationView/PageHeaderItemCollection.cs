// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Stores the distinct elements contributed by a navigation page to a host-defined header layout.
/// </summary>
/// <remarks>
/// This collection holds references without assigning logical or visual parents. A loaded panel
/// assigns those parents when it presents elements using <see cref="PanelItems.SourceProperty"/>.
/// Modify the collection on the UI thread. Null elements and duplicate references are rejected
/// before a mutation, because each element can occupy only one position in a panel.
/// </remarks>
public sealed class PageHeaderItemCollection : ObservableCollection<FrameworkElement>
{
    /// <summary>
    /// Creates an empty collection without taking ownership of any element's WPF tree position.
    /// </summary>
    public PageHeaderItemCollection()
    {
    }

    /// <summary>
    /// Validates and inserts an element, notifying the active header and its presenter.
    /// </summary>
    /// <param name="index">The insertion position.</param>
    /// <param name="item">The contributed element.</param>
    protected override void InsertItem(int index, FrameworkElement item)
    {
        ValidateItem(item, -1);
        base.InsertItem(index, item);
    }

    /// <summary>
    /// Validates and replaces an element, notifying the active header and its presenter.
    /// </summary>
    /// <param name="index">The position to replace.</param>
    /// <param name="item">The replacement element.</param>
    protected override void SetItem(int index, FrameworkElement item)
    {
        ValidateItem(item, index);
        base.SetItem(index, item);
    }

    /// <summary>
    /// Enforces the one-element-per-position contract before collection notifications are raised.
    /// </summary>
    private void ValidateItem(FrameworkElement item, int replacementIndex)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        item.Dispatcher.VerifyAccess();

        for (var index = 0; index < Count; index++)
        {
            if (index != replacementIndex && ReferenceEquals(this[index], item))
            {
                throw new ArgumentException(
                    "A header element can occur only once in the collection.",
                    nameof(item)
                );
            }
        }
    }
}
