// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Extensions.Wpf;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects the keyboard focus target when a dialog opens.
/// </summary>
public partial class ContentDialog
{
    /// <summary>
    /// Tries the explicit target, then marked visual descendants in depth-first order.
    /// If neither takes focus, preserves focus inside the dialog or searches the realized body
    /// for a tab stop, skipping Danger and Caution buttons. The final fallback is the dialog.
    /// </summary>
    protected internal virtual void SetInitialFocus()
    {
        ApplyTemplate();

        if (InitialFocus is DependencyObject target
            && ContentDialogHost.Contains(this, target)
            && TryFocus(InitialFocus))
        {
            return;
        }

        var markedTarget = this.FindDescendant<DependencyObject>(TryFocusMarkedElement);
        if (markedTarget is not null)
        {
            return;
        }

        if (ContentDialogHost.Contains(this, Keyboard.FocusedElement as DependencyObject))
        {
            return;
        }

        var presenter = GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
        presenter?.ApplyTemplate();

        var body = presenter as DependencyObject ?? Content as DependencyObject;
        if (body is IInputElement input && CanAutomaticallyFocus(body) && TryFocus(input))
        {
            return;
        }

        var first = body.FindDescendant<UIElement>(CanAutomaticallyFocus);
        if (first is null || !TryFocus(first))
        {
            Focus();
        }
    }

    /// <summary>
    /// Attempts focus during traversal so a rejected request leaves later candidates eligible.
    /// </summary>
    private static bool TryFocusMarkedElement(DependencyObject element)
    {
        return element is IInputElement input && GetIsInitialFocus(element) && TryFocus(input);
    }

    /// <summary>
    /// Checks the visibility and input state required for a visual to receive keyboard focus.
    /// </summary>
    private static bool CanFocus(DependencyObject element)
    {
        return element is UIElement { Focusable: true, IsVisible: true, IsEnabled: true }
               or UIElement3D { Focusable: true, IsVisible: true, IsEnabled: true };
    }

    // Appearance limits only the automatic choice. Explicit targets and later keyboard navigation
    // remain available so applications can deliberately focus an action that requires confirmation.
    private static bool CanAutomaticallyFocus(DependencyObject element)
    {
        return CanFocus(element)
               && KeyboardNavigation.GetIsTabStop(element)
               && element is not Button { Appearance: ControlAppearance.Danger or ControlAppearance.Caution };
    }

    /// <summary>
    /// Applies an explicit focus request without treating Tab participation as focus eligibility.
    /// Only automatic discovery requires tab participation.
    /// </summary>
    private static bool TryFocus(IInputElement element)
    {
        return element switch
        {
            UIElement ui when CanFocus(ui)                         => ui.Focus(),
            UIElement3D ui3D when CanFocus(ui3D)                   => ui3D.Focus(),
            ContentElement { Focusable: true, IsEnabled: true } ce => ce.Focus(),
            _                                                      => false,
        };
    }
}
