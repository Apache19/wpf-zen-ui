// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
    /// The menu is anchored to the last invoked key and never takes keyboard focus from the
    /// editor. It presents the current catalog snapshot without observing or changing input-mode
    /// state. Items remain disabled unless a valid editor currently owns keyboard focus.
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

        OpenInputMethodMenuCore(anchor, requestVersion);
    }

    /// <summary>
    /// Constructs the menu from the current catalog snapshot and rejects expired requests.
    /// </summary>
    private void OpenInputMethodMenuCore(UIElement anchor, int requestVersion)
    {
        if (requestVersion != _inputMethodMenuRequestVersion
            || !IsLoaded || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        if (requestVersion != _inputMethodMenuRequestVersion
            || !IsLoaded || !IsOpen || !IsEnabled || !IsVisible || !IsWindowForeground())
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement       = PlacementMode.Top,
            Focusable       = false
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
                CommandParameter = inputMethod,
                CommandTarget    = this,
                Focusable        = false,
                Command          = SoftKeyboardCommands.ActivateInputMethod
            };

            AutomationProperties.SetName(
                item, $"{inputMethod.LanguageDisplayName}, {inputMethod.DisplayName}"
            );

            menu.Items.Add(item);
        }

        _inputMethodMenu = menu;

        menu.IsOpen = menu.Items.Count > 0;
    }

    /// <summary>
    /// Gets whether the current focused editor can own a focusless input-method menu operation.
    /// </summary>
    /// <remarks>
    /// <see cref="ActiveTarget"/> remembers the last supported editor by design. Menu availability
    /// additionally requires that editor to own keyboard focus now, so a pointer action elsewhere
    /// cannot reactivate a remembered target or change its input method.
    /// </remarks>
    private bool IsInputMethodMenuAvailable
    {
        get
        {
            var target  = ActiveTarget;
            var adapter = target is null ? null : GetAdapter(target);

            return IsLoaded && IsOpen && IsEnabled && IsVisible
                && target is DependencyObject targetObject
                && ReferenceEquals(Keyboard.FocusedElement, target)
                && adapter is not null
                && IsWithinEffectiveScope(targetObject)
                && adapter.CanReceiveInput(target)
                && IsWindowForeground();
        }
    }
}
