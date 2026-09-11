// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides header presentation and the keyboard-wide input-method menu.
/// </summary>
public partial class SoftKeyboard
{
    private ContextMenu? _inputMethodMenu;

    /// <summary>
    /// Opens the installed input-method menu without replacing or resizing the keyboard.
    /// </summary>
    /// <remarks>
    /// The menu is anchored to the last invoked key.
    /// Selection restores editor focus before activation.
    /// </remarks>
    public void ShowInputMethodMenu()
    {
        if (!IsEnabled || !IsOpen || !IsVisible)
        {
            return;
        }

        UIElement anchor = _inputMethodMenuAnchor ?? this;
        RunAfterActiveCompositionCancellation(() => OpenInputMethodMenuCore(anchor));
    }

    private void OpenInputMethodMenuCore(UIElement anchor)
    {
        if (!IsLoaded || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        CloseFlyouts();
        RefreshInputMethods();

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Top
        };

        foreach (SoftKeyboardInputMethod inputMethod in InputMethods)
        {
            var item = new MenuItem
            {
                Header = inputMethod.DisplayName,
                IsCheckable = true,
                IsChecked = inputMethod.IsActive
            };

            item.Click += (_, _) =>
            {
                // Close the menu first: focus restoration and composition cancellation must target
                // the editor's TSF document, not the menu's temporary focus scope.
                menu.IsOpen = false;
                if (PrepareTargetForInput())
                {
                    ActivateInputMethod(inputMethod);
                }
            };

            menu.Items.Add(item);
        }

        _inputMethodMenu = menu;
        menu.IsOpen = menu.Items.Count > 0;
    }
}
