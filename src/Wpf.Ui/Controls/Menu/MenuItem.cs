// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

using System.Windows.Input;

namespace Wpf.Ui.Controls;

/// <summary>
/// Extended <see cref="System.Windows.Controls.MenuItem"/> with <see cref="SymbolRegular"/> properties.
/// </summary>
public class MenuItem : System.Windows.Controls.MenuItem
{
    static MenuItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MenuItem),
            new FrameworkPropertyMetadata(typeof(MenuItem))
        );

        IconProperty.OverrideMetadata(
            typeof(MenuItem),
            new FrameworkPropertyMetadata(null)
        );
    }

    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0012:CLR property type should match registered type",
        Justification = "seems harmless"
    )]
    public new IconElement Icon
    {
        get => (IconElement)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc/>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        System.Windows.Controls.MenuItem.PrepareMenuItem(element, item);
    }

    /// <summary>
    ///     Automatically set the Command property if the data item that this MenuItem represents is a command.
    /// </summary>
    internal static void PrepareMenuItem(DependencyObject element, object item)
    {
        MenuItem menuItem = element as MenuItem;
        if (menuItem != null)
        {
            ICommand command = item as ICommand;
            if (command != null)
            {
                if (!menuItem.HasNonDefaultValue(CommandProperty))
                {
                    menuItem.Command = command;
                }
            }

            if (MenuItem.GetBoolField(menuItem, BoolField.CanExecuteInvalid))
            {
                menuItem.UpdateCanExecute();
            }
        }
        else
        {
            Separator separator = item as Separator;
            if (separator != null)
            {
                bool                    hasModifiers;
                BaseValueSourceInternal vs = separator.GetValueSource(StyleProperty, null, out hasModifiers);
                if (vs <= BaseValueSourceInternal.ImplicitReference)
                    separator.SetResourceReference(StyleProperty, SeparatorStyleKey);

                separator.DefaultStyleKey = SeparatorStyleKey;
            }
        }
    }
}