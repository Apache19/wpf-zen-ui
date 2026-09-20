// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Presents immutable keyboard keys using the key controls themselves as item containers.
/// </summary>
/// <remarks>
/// Items must be <see cref="SoftKeyboardKey"/> instances. Container preparation applies their key,
/// content, automation name, and <see cref="SoftKeyboardPanel"/> placement directly, avoiding an
/// outer content presenter and per-item placement bindings.
/// Use <see cref="ItemsControl.ItemsPanel"/> to choose the panel,
/// <see cref="ItemsControl.ItemContainerStyle"/> to style key controls, and
/// <see cref="ItemsControl.ItemTemplate"/> to customize the presentation of each key's content.
/// </remarks>
public class SoftKeyboardKeyList : ItemsControl
{
    /// <summary>
    /// Requires a generated container so each item follows the immutable key preparation contract.
    /// </summary>
    /// <param name="item">The proposed keyboard item.</param>
    /// <returns>Always false.</returns>
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return false;
    }

    /// <summary>
    /// Creates the interactive key control used as the direct child of the items panel.
    /// </summary>
    /// <returns>A key control whose template is applied by WPF during measurement.</returns>
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new SoftKeyboardKeyControl();
    }

    /// <summary>
    /// Applies one immutable key snapshot before its container is measured.
    /// </summary>
    /// <param name="element">The generated key control.</param>
    /// <param name="item">The key description to present.</param>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is not SoftKeyboardKeyControl container || item is not SoftKeyboardKey key)
        {
            throw new InvalidOperationException(
                $"{nameof(SoftKeyboardKeyList)} items must be {nameof(SoftKeyboardKey)} instances."
            );
        }

        base.PrepareContainerForItemOverride(element, item);

        container.SetCurrentValue(SoftKeyboardKeyControl.KeyProperty, key);
        container.SetCurrentValue(ContentControl.ContentProperty, key.Content);

        AutomationProperties.SetName(container, key.AutomationName);
        SoftKeyboardPanel.SetRow(container, key.Row);
        SoftKeyboardPanel.SetColumn(container, key.Column);
        SoftKeyboardPanel.SetColumnSpan(container, key.ColumnSpan);
    }

    /// <summary>
    /// Cancels held-key interaction and releases the snapshot when a container is removed.
    /// </summary>
    /// <param name="element">The outgoing key control.</param>
    /// <param name="item">The previously presented key description.</param>
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is SoftKeyboardKeyControl container)
        {
            container.SetCurrentValue(SoftKeyboardKeyControl.KeyProperty, null);
            container.ClearValue(ContentControl.ContentProperty);
            container.ClearValue(AutomationProperties.NameProperty);
            container.ClearValue(SoftKeyboardPanel.RowProperty);
            container.ClearValue(SoftKeyboardPanel.ColumnProperty);
            container.ClearValue(SoftKeyboardPanel.ColumnSpanProperty);
        }

        base.ClearContainerForItemOverride(element, item);
    }
}
