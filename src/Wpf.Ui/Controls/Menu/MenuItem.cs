// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using Wpf.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extended <see cref="System.Windows.Controls.MenuItem"/> with <see cref="SymbolRegular"/> properties.
/// </summary>
public class MenuItem : System.Windows.Controls.MenuItem
{
    // Cache the FrameworkElement.DefaultStyleKey property via reflection.
    //
    // DefaultStyleKey has a protected setter, which prevents assigning a new
    // value to arbitrary control instances from outside the inheritance chain.
    // In this library, we need to adjust the DefaultStyleKey of Separator
    // instances created by the base MenuItem implementation in order to redirect
    // theme resolution to our ComponentResourceKey.
    //
    // Reflection is used here to safely override the default style identity
    // without requiring a custom Separator subclass for this specific case.
    private static readonly PropertyInfo? DefaultStyleKeyPropertyInfo =
        typeof(FrameworkElement).GetProperty(
            "DefaultStyleKey",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

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

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        // If the item is a MenuItem, delegate to the base implementation.
        // We only intercept Separator instances to adjust their styling behavior.
        if (item is MenuItem)
        {
            base.PrepareContainerForItemOverride(element, item);
            return;
        }

        PrepareSeparator(element, item);
    }

    private static void PrepareSeparator(DependencyObject element, object item)
    {
        if (item is Separator separator)
        {
            // The base Menu implementation assigns a default Style to menu Separators
            // using a SystemResourceKey. This bypasses the library's Generic.xaml,
            // because the style lookup no longer flows through our resource dictionaries.
            //
            // To restore theme resolution within this library (especially in scenarios
            // without an Application object), we replace the default style reference
            // with a ComponentResourceKey defined by this library.

            var source = DependencyPropertyHelper.GetValueSource(
                separator,
                StyleProperty
            );

            // Mirror the base class logic: only override the Style if it has not been
            // explicitly set by the user. This preserves user-defined styles.
            var vs = source.BaseValueSource;
            if (vs <= BaseValueSource.ImplicitStyleReference)
            {
                separator.SetResourceReference(
                    StyleProperty,
                    StyleKeys.UiMenuItemSeparatorStyle
                );
            }

            // Update DefaultStyleKey to ensure the control template is resolved
            // against this library's theme resources instead of the system key.
            DefaultStyleKeyPropertyInfo?.SetValue(
                separator,
                StyleKeys.UiMenuItemSeparatorStyle
            );
        }
    }
}