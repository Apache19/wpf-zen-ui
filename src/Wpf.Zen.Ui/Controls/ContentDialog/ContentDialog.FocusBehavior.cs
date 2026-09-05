// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Extensions.Wpf;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Provides content-first initial focus without interpreting button appearance.</summary>
public partial class ContentDialog
{
    /// <summary>
    /// Focuses InitialFocus when it is an enabled, visible dialog descendant; otherwise respects
    /// focus already inside, searches the realized body, and finally focuses the dialog itself.
    /// Footer appearance never determines focus. The host calls this after templates are realized.
    /// </summary>
    protected internal virtual void SetInitialFocus()
    {
        ApplyTemplate();

        if (InitialFocus is DependencyObject target && ContentDialogHost.Contains(this, target)
            && TryFocus(InitialFocus))
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
        if (body is IInputElement input && TryFocus(input))
        {
            return;
        }

        var first = body.FindDescendant<UIElement>(CanFocus);
        if (first is null || !TryFocus(first))
        {
            Focus();
        }
    }

    private static bool CanFocus(DependencyObject element) =>
        element is UIElement { Focusable: true, IsVisible: true, IsEnabled: true }
        && element is not Control { IsTabStop: false };

    private static bool TryFocus(IInputElement element) => element switch
    {
        UIElement ui when CanFocus(ui) => ui.Focus(),
        ContentElement { Focusable: true, IsEnabled: true } content => content.Focus(),
        _ => false,
    };
}
