// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Provides header presentation and the keyboard-wide input-method menu.
/// </summary>
public partial class SoftKeyboard
{
    private ContextMenu? _inputMethodMenu;
    private int          _inputMethodMenuRequestVersion;

    /// <summary>
    /// Opens the installed input-method menu without replacing or resizing the keyboard.
    /// </summary>
    /// <remarks>
    /// The menu is anchored to the last invoked key.
    /// The catalog is refreshed before opening. A closed, superseded, or expired request
    /// stays closed.
    /// Selection restores editor focus before activation.
    /// </remarks>
    public void ShowInputMethodMenu()
    {
        VerifyAccess();

        if (!IsEnabled || !IsOpen || !IsVisible)
        {
            return;
        }

        var anchor = _inputMethodMenuAnchor ?? this;

        CloseFlyouts();

        var requestVersion = _inputMethodMenuRequestVersion;

        RunAfterActiveCompositionCancellation(
            () => OpenInputMethodMenuCore(anchor, requestVersion)
        );
    }

    /// <summary>
    /// Reads the catalog before constructing the menu and rejects reentrant source or
    /// target changes.
    /// </summary>
    private void OpenInputMethodMenuCore(UIElement anchor, int requestVersion)
    {
        if (requestVersion != _inputMethodMenuRequestVersion
            || !IsLoaded || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        var operationVersion = _inputOperationVersion;
        var sessionVersion   = _inputMethodSessionVersion;

        if (!RefreshInputMethods()
            || requestVersion != _inputMethodMenuRequestVersion
            || operationVersion != _inputOperationVersion
            || sessionVersion != _inputMethodSessionVersion
            || !IsLoaded || !IsOpen || !IsEnabled || !IsVisible || !IsWindowForeground())
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement       = PlacementMode.Top
        };

        foreach (var inputMethod in InputMethods)
        {
            var header = new StackPanel();

            header.Children.Add(
                new TextBlock
                {
                    Text       = inputMethod.LanguageDisplayName,
                    FontWeight = FontWeights.SemiBold,
                }
            );

            header.Children.Add(
                new TextBlock
                {
                    Text    = inputMethod.DisplayName,
                    Margin  = new Thickness(0, 2, 0, 0),
                    Opacity = 0.75,
                }
            );

            var item = new MenuItem
            {
                Header           = header,
                IsCheckable      = true,
                IsChecked        = inputMethod.IsActive,
                Command          = SoftKeyboardCommands.ActivateInputMethod,
                CommandParameter = inputMethod,
                CommandTarget    = this
            };

            AutomationProperties.SetName(
                item, $"{inputMethod.LanguageDisplayName}, {inputMethod.DisplayName}"
            );

            menu.Items.Add(item);
        }

        _inputMethodMenu = menu;

        menu.Closed += (_, _) => QueueInputMethodStateRefresh();

        menu.IsOpen = menu.Items.Count > 0;
    }
}