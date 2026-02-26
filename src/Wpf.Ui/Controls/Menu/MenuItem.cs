// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
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
        // Each MenuItem is responsible for ensuring that its immediate child
        // containers receive the correct default styling within this theme system.
        //
        // We only intervene for two specific cases:
        // 1) Native WPF MenuItem instances, which do not automatically resolve
        //    styles from this library's Generic.xaml in certain hosting scenarios.
        // 2) Separator instances, whose default style is internally redirected
        //    to a system resource key by the base implementation.

        if (item is System.Windows.Controls.MenuItem menuItem)
        {
            // Apply the library's default style only to standard (unmodified)
            // WPF MenuItem instances.
            //
            // Custom subclasses or explicitly styled MenuItems are intentionally
            // left untouched to avoid overriding user-defined behavior.
            //
            // Note: The library's own MenuItem type resolves its style automatically,
            // so no additional action is required in that case.
            if (IsStandardMenuItem(menuItem))
            {
                ApplyThemeStyling(menuItem, StyleKeys.MenuItemStyle);
            }

            // Allow the base class to perform its normal container preparation logic.
            base.PrepareContainerForItemOverride(element, item);
            return;
        }

        // Handle Separator instances separately.
        // The base WPF implementation assigns a system-level style key to
        // menu Separators, which bypasses this library's theme resources.
        // We redirect their style resolution so that the library's theme
        // (ComponentResourceKey-based) styling can take effect.
        PrepareSeparator(element, item);
    }

    internal static bool IsStandardMenuItem(System.Windows.Controls.MenuItem item)
    {
        return item.GetType() == typeof(System.Windows.Controls.MenuItem);
    }

    internal static void PrepareSeparator(DependencyObject element, object item)
    {
        if (item is System.Windows.Controls.Separator separator)
        {
            // The base Menu implementation assigns a default Style to menu Separators
            // using a SystemResourceKey. This bypasses the library's Generic.xaml,
            // because the style lookup no longer flows through our resource dictionaries.
            //
            // To restore theme resolution within this library (especially in scenarios
            // without an Application object), we replace the default style reference
            // with a ComponentResourceKey defined by this library.

            ApplyThemeStyling(separator, StyleKeys.MenuItemSeparatorStyle);
        }
    }

    internal static void ApplyThemeStyling(FrameworkElement element, ComponentResourceKey styleResourceKey)
    {
        var source = DependencyPropertyHelper.GetValueSource(
            element,
            StyleProperty
        );

        // Mirror the base class logic: only override the Style if it has not been
        // explicitly set by the user. This preserves user-defined styles.
        var vs = source.BaseValueSource;
        if (vs <= BaseValueSource.ImplicitStyleReference)
        {
            element.SetResourceReference(StyleProperty, styleResourceKey);
        }

        // Update DefaultStyleKey to ensure the control template is resolved
        // against this library's theme resources instead of the system key.
        DefaultStyleKeyPropertyInfo?.SetValue(element, styleResourceKey);
    }
}