// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using Button = Wpf.Zen.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Zen.Ui.Controls.TextBox;

namespace Wpf.Zen.Ui.Demo.Simple.Views.Pages;

/// <summary>
/// Demonstrates direct control composition and event-driven dialog results without a view model.
/// </summary>
public partial class DashboardPage
{
    private async void OnInformationClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Header = "Changes saved",
            MaxWidth = 400,
            Content = new TextBlock
            {
                Text = "Your changes have been saved. You can continue working.",
                TextWrapping = TextWrapping.Wrap,
            },
        };
        var actions = new ContentDialogActionBar();
        var close = new Button { Content = "Got it", IsDefault = true };
        close.Click += (_, _) => dialog.Close(true);
        actions.Items.Add(close);
        dialog.Footer = actions;

        object? result = await ShowDialogAsync(dialog).ConfigureAwait(true);
        DialogResultText.Text = result is true ? "Information acknowledged." : "Information dismissed.";
    }

    private async void OnConfirmationClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Header = "Delete this item?",
            MaxWidth = 420,
            Content = new TextBlock
            {
                Text = "This example only returns a choice; it does not delete any files.",
                TextWrapping = TextWrapping.Wrap,
            },
        };
        var actions = new ContentDialogActionBar();
        var delete = new Button { Content = "Delete", Appearance = ControlAppearance.Danger };
        delete.Click += (_, _) => dialog.Close(true);
        var cancel = new Button { Content = "Cancel", IsDefault = true, IsCancel = true };
        cancel.Click += (_, _) => dialog.Close();
        actions.Items.Add(delete);
        actions.Items.Add(cancel);
        dialog.Footer = actions;

        object? result = await ShowDialogAsync(dialog).ConfigureAwait(true);
        DialogResultText.Text = result is true ? "Deletion confirmed (simulation only)." : "Deletion canceled.";
    }

    private async void OnInputClick(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { MinWidth = 240, MaxLength = 80 };
        var error = new TextBlock
        {
            Text = "Enter a non-empty name.",
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "Display name", Margin = new Thickness(0, 0, 0, 8) });
        content.Children.Add(name);
        content.Children.Add(error);

        var dialog = new ContentDialog
        {
            Header = "Edit display name",
            MaxWidth = 420,
            Content = content,
        };
        var actions = new ContentDialogActionBar();
        var save = new Button { Content = "Save", Appearance = ControlAppearance.Accent, IsDefault = true };
        save.Click += (_, _) =>
        {
            // Keep the dialog open until validation succeeds. The result is the input value,
            // not a view model or a reference to a control whose presentation has ended.
            string result = name.Text.Trim();
            if (result.Length == 0)
            {
                error.Visibility = Visibility.Visible;
                name.Focus();
                return;
            }

            dialog.Close(result);
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => dialog.Close();
        actions.Items.Add(save);
        actions.Items.Add(cancel);
        dialog.Footer = actions;

        object? result = await ShowDialogAsync(dialog).ConfigureAwait(true);
        DialogResultText.Text = result is string value ? $"Display name: {value}" : "Editing canceled.";
    }

    /// <summary>
    /// Uses the containing WPF surface as the modal scope, without requiring a Window or DI service.
    /// </summary>
    private Task<object?> ShowDialogAsync(ContentDialog dialog)
    {
        // An ElementHost has no WPF Window. Walk the visual ancestors so the same example also
        // works in WinForms, and choose the nearest host when the UI contains nested scopes.
        for (DependencyObject? ancestor = this; ancestor is not null; ancestor = VisualTreeHelper.GetParent(ancestor))
        {
            if (ancestor is ContentDialogHost host)
            {
                return host.ShowAsync(dialog);
            }
        }

        throw new InvalidOperationException("Place the demo content inside a ContentDialogHost.");
    }
}
