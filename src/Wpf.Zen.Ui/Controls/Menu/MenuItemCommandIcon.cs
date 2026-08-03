// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides an attached property that automatically sets a <see cref="SymbolIcon"/>
/// on a <see cref="MenuItem"/> when its <see cref="MenuItem.Command"/> matches a
/// well-known built-in WPF command (Cut, Copy, Paste, Delete, etc.).
/// <para>
/// Enable by setting <c>MenuItemCommandIcon.AutoSetIcon="True"</c> on the MenuItem,
/// or apply it globally via the MenuItem style.
/// </para>
/// </summary>
public static class MenuItemCommandIcon
{
    /// <summary>
    /// Identifies the AutoSetIcon attached property.
    /// </summary>
    public static readonly DependencyProperty AutoSetIconProperty =
        DependencyProperty.RegisterAttached(
            "AutoSetIcon",
            typeof(bool),
            typeof(MenuItemCommandIcon),
            new PropertyMetadata(false, OnAutoSetIconChanged)
        );

    /// <summary>
    /// Maps well-known <see cref="RoutedCommand"/> singletons to their corresponding
    /// <see cref="SymbolRegular"/> icon.
    /// </summary>
    private static readonly Dictionary<RoutedCommand, SymbolRegular> CommandIconMap = new()
    {
        // ApplicationCommands
        [ApplicationCommands.Cut] = SymbolRegular.Cut24,
        [ApplicationCommands.Copy] = SymbolRegular.Copy24,
        [ApplicationCommands.Paste] = SymbolRegular.ClipboardPaste24,
        [ApplicationCommands.Delete] = SymbolRegular.Delete24,
        [ApplicationCommands.Undo] = SymbolRegular.ArrowUndo24,
        [ApplicationCommands.Redo] = SymbolRegular.ArrowRedo24,
        [ApplicationCommands.SelectAll] = SymbolRegular.SelectAllOn24,
        [ApplicationCommands.Open] = SymbolRegular.Open24,
        [ApplicationCommands.Save] = SymbolRegular.Save24,
        [ApplicationCommands.SaveAs] = SymbolRegular.Save24,
        [ApplicationCommands.Close] = SymbolRegular.Dismiss24,
        [ApplicationCommands.New] = SymbolRegular.New24,
        [ApplicationCommands.Print] = SymbolRegular.Print24,
        [ApplicationCommands.Find] = SymbolRegular.Search24,
        [ApplicationCommands.Replace] = SymbolRegular.Search24,

        // NavigationCommands
        [NavigationCommands.Refresh] = SymbolRegular.ArrowClockwise24,

        // EditingCommands
        [EditingCommands.ToggleBold] = SymbolRegular.TextBold24,
        [EditingCommands.ToggleItalic] = SymbolRegular.TextItalic24,
        [EditingCommands.ToggleUnderline] = SymbolRegular.TextUnderline24,
    };

    /// <summary>
    /// Gets the value of the AutoSetIcon attached property for a given dependency object.
    /// </summary>
    [AttachedPropertyBrowsableForType(typeof(MenuItem))]
    public static bool GetAutoSetIcon(DependencyObject element)
    {
        return (bool)element.GetValue(AutoSetIconProperty);
    }

    /// <summary>
    /// Sets the value of the AutoSetIcon attached property for a given dependency object.
    /// </summary>
    [AttachedPropertyBrowsableForType(typeof(MenuItem))]
    public static void SetAutoSetIcon(DependencyObject element, bool value)
    {
        element.SetValue(AutoSetIconProperty, value);
    }

    /// <summary>
    /// Attempts to set the <see cref="MenuItem.Icon"/> property to a matching
    /// <see cref="SymbolIcon"/> when the <see cref="MenuItem.Command"/> is a
    /// recognized built-in command and the Icon has not already been set by the user.
    /// </summary>
    /// <param name="menuItem">The menu item to inspect and potentially decorate.</param>
    /// <returns><c>true</c> if an icon was assigned; otherwise <c>false</c>.</returns>
    public static bool TrySetIcon(MenuItem menuItem)
    {
        if (menuItem.Icon is not null)
        {
            return false;
        }

        if (menuItem.Command is RoutedCommand routedCommand &&
            CommandIconMap.TryGetValue(routedCommand, out var symbol))
        {
            menuItem.SetCurrentValue(MenuItem.IconProperty, new SymbolIcon(symbol, fontSize: 16));
            return true;
        }

        return false;
    }

    private static void OnAutoSetIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MenuItem menuItem)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            menuItem.Loaded += OnMenuItemLoaded;
        }
        else
        {
            menuItem.Loaded -= OnMenuItemLoaded;
        }
    }

    private static void OnMenuItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            TrySetIcon(menuItem);
        }
    }
}
