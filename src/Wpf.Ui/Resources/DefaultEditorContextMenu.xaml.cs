// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Ui.Resources;

/// <summary>
/// Provides the shared resource definitions used by the default text-editor context menu.
/// </summary>
public partial class DefaultEditorContextMenu : ResourceDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEditorContextMenu"/> class.
    /// </summary>
    public DefaultEditorContextMenu()
    {
        // ResourceDictionary.Source constructs this class while its BAML is already being loaded.
        // Calling InitializeComponent here would start a second load into the same dictionary and
        // fail with "Cannot re-initialize ResourceDictionary instance." This mirrors the existing
        // ContextMenuLoader dictionary, which is also instantiated through a pack URI.
    }

    private void OnDefaultEditorContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { PlacementTarget: { } commandTarget } contextMenu)
        {
            return;
        }

        // RoutedCommand evaluates CanExecute while the menu is open, but MenuItem intentionally
        // closes the menu and defers Execute until DispatcherPriority.Render. A ContextMenu lives
        // in a separate presentation source, and its route back to PlacementTarget may already be
        // gone by that point. WPF's internal TextEditorContextMenu avoids this race by assigning
        // TextEditor.UiScope to every generated MenuItem.CommandTarget. Snapshot the equivalent
        // public PlacementTarget on each opening so execution remains directed at the editor even
        // after the popup closes. Reassigning on every opening also supports a menu instance that a
        // consumer deliberately moves between editor controls.
        foreach (var item in contextMenu.Items)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.SetCurrentValue(MenuItem.CommandTargetProperty, commandTarget);
            }
        }
    }
}
