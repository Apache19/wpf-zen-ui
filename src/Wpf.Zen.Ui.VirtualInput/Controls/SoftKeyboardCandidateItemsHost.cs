// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Owns WPF item-container generation for <see cref="SoftKeyboardCandidateList"/> without exposing
/// a general-purpose <see cref="ItemsControl.ItemsSource"/> contract on the public control.
/// </summary>
internal sealed class SoftKeyboardCandidateItemsHost : ItemsControl
{
    private readonly SoftKeyboardCandidateList _owner;

    internal SoftKeyboardCandidateItemsHost(SoftKeyboardCandidateList owner)
    {
        _owner = owner;

        Focusable = false;
        IsTabStop = false;

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        ItemsPanel = new ItemsPanelTemplate(panel);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return false;
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new SoftKeyboardCandidateItem();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        var container = (SoftKeyboardCandidateItem)element;
        container.SetCandidate((SoftKeyboardCandidate)item);

        _owner.UpdateSelection(container);
        _owner.QueueReveal();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        var container = (SoftKeyboardCandidateItem)element;

        container.SetCandidate(null);
        container.SetIsSelected(false);

        if (container.IsMouseCaptured)
        {
            container.ReleaseMouseCapture();
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        _owner.QueueReveal();
    }
}
