// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Appearance;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui;

/// <summary>
/// Provides attached properties for controlling theme behavior on individual elements.
/// </summary>
public static class Theme
{
    static Theme()
    {
        LibraryInitializer.EnsureInitialized();
    }

    /// <summary>
    /// Identifies the ApplyToRootVisual attached property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This optional property registers the owning presentation root as an explicit element-resource target.
    /// The active theme dictionary is copied into that root's <see cref="FrameworkElement.Resources"/> collection.
    /// </para>
    /// <para>
    /// It is not required for global theme switching, including WinForms <c>ElementHost</c> scenarios without an
    /// <see cref="Application"/>. ThemeManager hosts global resources through WPF's system-theme mechanism and detects
    /// new presentation roots automatically. Use this property only when a root intentionally needs its own resource
    /// scope; use <see cref="RequestedProperty"/> for a local Light/Dark theme that differs from the global theme.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code lang="xml">
    /// &lt;UserControl ui:Theme.ApplyToRootVisual="True" /&gt;
    /// </code>
    /// </example>
    public static readonly DependencyProperty ApplyToRootVisualProperty =
        DependencyProperty.RegisterAttached(
            "ApplyToRootVisual",
            typeof(bool),
            typeof(Theme),
            new PropertyMetadata(
                false,
                static (d, e) => ThemeManager.OnApplyToRootVisualChanged(d, e)
            )
        );

    /// <summary>
    /// Helper for getting <see cref="ApplyToRootVisualProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ApplyToRootVisualProperty"/> from.
    /// </param>
    /// <returns>ApplyToRootVisual property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetApplyToRootVisual(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(ApplyToRootVisualProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ApplyToRootVisualProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ApplyToRootVisualProperty"/> on.
    /// </param>
    /// <param name="value">ApplyToRootVisual property value.</param>
    public static void SetApplyToRootVisual(DependencyObject element, bool value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(ApplyToRootVisualProperty, value);
    }

    /// <summary>
    /// Identifies the Requested attached property.
    /// </summary>
    /// <remarks>
    /// Specifies the requested theme mode for the element on which it is set.
    ///
    /// When applied, the theme resources corresponding to the specified
    /// mode are injected into the resource scope of that element.
    /// Descendant elements resolve theme resources through the normal
    /// WPF resource lookup mechanism.
    ///
    /// This setting is local in scope and does not modify the global
    /// <see cref="ThemeManager.ThemeMode"/>.
    ///
    /// The final applied theme may still be affected by system policies
    /// such as high contrast mode. When set to <see cref="UiThemeMode.System"/>,
    /// the requested mode is resolved to either Light or Dark
    /// based on the current system setting.
    /// </remarks>
    /// <example>
    /// <code lang="xml">
    /// &lt;Border ui:Theme.Requested="Dark" /&gt;
    /// </code>
    /// </example>
    public static readonly DependencyProperty RequestedProperty =
        DependencyProperty.RegisterAttached(
            "Requested",
            typeof(UiThemeMode),
            typeof(Theme),
            new FrameworkPropertyMetadata(
                UiThemeMode.Default,
                static (d, e) => ThemeManager.OnRequestedThemeChanged(d, e)
            )
        );

    /// <summary>
    /// Helper for getting <see cref="RequestedProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="RequestedProperty"/> from.
    /// </param>
    /// <returns>Requested property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UiThemeMode GetRequested(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (UiThemeMode)element.GetValue(RequestedProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="RequestedProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="RequestedProperty"/> on.
    /// </param>
    /// <param name="value">Requested property value.</param>
    public static void SetRequested(DependencyObject element, UiThemeMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(RequestedProperty, value);
    }
}
