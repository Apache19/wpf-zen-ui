// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines opt-in suppression of WPF's legacy browser-navigation input for
/// <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// Installs command-boundary handlers for navigation commands that can arrive without a raw
    /// key or mouse event, notably commands translated from <c>WM_APPCOMMAND</c>.
    /// </summary>
    /// <remarks>
    /// The bindings remain passive while their corresponding
    /// <see cref="SuppressedNavigationInput"/> flag is absent. This allows a page or an ancestor to
    /// provide deliberate application semantics without the control silently taking ownership of
    /// those commands.
    /// </remarks>
    private void InitializeNavigationInputSuppression()
    {
        CommandBindings.Add(CreateNavigationInputSuppressionBinding(NavigationCommands.Refresh));
        CommandBindings.Add(CreateNavigationInputSuppressionBinding(NavigationCommands.BrowseBack));
        CommandBindings.Add(CreateNavigationInputSuppressionBinding(NavigationCommands.BrowseForward));
    }

    private CommandBinding CreateNavigationInputSuppressionBinding(RoutedCommand command) =>
        new(command, OnNavigationInputExecuted, OnNavigationInputCanExecute);

    /// <summary>
    /// Suppresses key-down and key-up independently so a page cannot accidentally observe half of
    /// an input sequence when it handles one phase rather than the other.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5 && IsNavigationInputSuppressed(NavigationViewInput.Refresh))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc cref="OnPreviewKeyDown"/>
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.F5 && IsNavigationInputSuppressed(NavigationViewInput.Refresh))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyUp(e);
    }

    /// <summary>
    /// Suppresses side-button input on the tunneling route, before the navigated page can interpret
    /// it. Both down and up are covered for the same reason as keyboard input.
    /// </summary>
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (IsNavigationMouseButtonSuppressed(e.ChangedButton))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseDown(e);
    }

    /// <inheritdoc cref="OnPreviewMouseDown"/>
    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        if (IsNavigationMouseButtonSuppressed(e.ChangedButton))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseUp(e);
    }

    private void OnNavigationInputCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (
            !TryGetNavigationInput(e.Command, out NavigationViewInput input)
            || !IsNavigationInputSuppressed(input)
        )
        {
            return;
        }

        // CommandBinding invokes its Executed handler only when the same binding reports that the
        // command can execute. Report true here so the no-op Executed handler becomes the terminal
        // command target. It marks the routed command handled without performing navigation; this
        // is also how the originating keyboard or WM_APPCOMMAND input becomes handled.
        e.CanExecute = true;
        e.Handled = true;
    }

    private void OnNavigationInputExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (
            TryGetNavigationInput(e.Command, out NavigationViewInput input)
            && IsNavigationInputSuppressed(input)
        )
        {
            e.Handled = true;
        }
    }

    private bool IsNavigationMouseButtonSuppressed(MouseButton button) =>
        button switch
        {
            MouseButton.XButton1 => IsNavigationInputSuppressed(NavigationViewInput.BrowserBack),
            MouseButton.XButton2 => IsNavigationInputSuppressed(NavigationViewInput.BrowserForward),
            _ => false,
        };

    private bool IsNavigationInputSuppressed(NavigationViewInput input) =>
        (SuppressedNavigationInput & input) != NavigationViewInput.None;

    private static bool TryGetNavigationInput(ICommand command, out NavigationViewInput input)
    {
        if (command == NavigationCommands.Refresh)
        {
            input = NavigationViewInput.Refresh;
            return true;
        }

        if (command == NavigationCommands.BrowseBack)
        {
            input = NavigationViewInput.BrowserBack;
            return true;
        }

        if (command == NavigationCommands.BrowseForward)
        {
            input = NavigationViewInput.BrowserForward;
            return true;
        }

        input = NavigationViewInput.None;
        return false;
    }
}
