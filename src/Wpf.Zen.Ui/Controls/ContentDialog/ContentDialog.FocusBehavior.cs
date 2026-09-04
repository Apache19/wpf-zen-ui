// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Extensions.Wpf;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Contains modal focus containment and content-first keyboard initialization.</summary>
public partial class ContentDialog
{
    /// <summary>Gets a value indicating whether keyboard focus belongs to the dialog, including logical Popup descendants.</summary>
    public bool IsFocusInsideDialog()
    {
        VerifyAccess();
        return IsFocusInsideDialogCore(Keyboard.FocusedElement);
    }

    /// <inheritdoc />
    protected override void OnPreviewLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnPreviewLostKeyboardFocus(e);

        // After Content changes, the old visual can remain loaded until the next layout pass.
        // Only the currently hosted dialog may retain focus; otherwise it would trap its successor.
        if (IsOpen && ReferenceEquals(_dialogHost?.Content, this) && IsLoaded && IsVisible
            && e.NewFocus is not null && Window.GetWindow(this) is not { IsActive: false }
            && !IsFocusInsideDialogCore(e.NewFocus))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Bubble, rather than preview, lets editors and popups consume Escape first.
        if (!e.Handled && e.Key == Key.Escape && CanInteract && IsDismissible)
        {
            Hide();
            e.Handled = true;
        }
    }

    private bool IsFocusInsideDialogCore(IInputElement? element)
    {
        for (DependencyObject? current = element as DependencyObject; current is not null; current = current.GetParent())
        {
            if (ReferenceEquals(current, this))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Focuses realized user content first, then the explicit default action, another safe action,
    /// or the dialog itself. No implicit default is assigned when the requested action is unavailable.
    /// </summary>
    protected virtual void SetInitialFocus()
    {
        ApplyTemplate();

        // Searching the presenter covers both direct UIElement content (including its root) and
        // controls created by ContentTemplate; searching only Content misses view-model-driven forms.
        var presenter = GetTemplateChild("PART_ContentPresenter") as ContentPresenter;

        presenter?.ApplyTemplate();

        var content = presenter as DependencyObject ?? Content as DependencyObject;
        if (content is UIElement element && IsSafelyFocusable(element) && element.Focus())
            return;

        var input = content.FindDescendant<Control>(IsSafelyFocusable);
        if (input?.Focus() == true)
            return;

        var buttons = new[] { "PART_PrimaryButton", "PART_SecondaryButton", "PART_TertiaryButton", "PART_CloseButton" }
            .Select(name => GetTemplateChild(name)).OfType<System.Windows.Controls.Button>()
            .Where(IsSafelyFocusable).ToArray();

        var button = buttons.FirstOrDefault(b => b.IsDefault) ?? buttons.FirstOrDefault();
        if (button?.Focus() != true)
            Focus();
    }

    private static bool IsSafelyFocusable(DependencyObject? element)
    {
        return element is UIElement { Focusable: true, IsVisible: true, IsEnabled: true }
               && element is not Control { IsTabStop: false }
               && element is not Button { Appearance: ControlAppearance.Danger or ControlAppearance.Caution };
    }
}
