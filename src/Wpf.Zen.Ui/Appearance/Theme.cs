// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Appearance;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui;

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
    /// <para>
    /// Specifies the theme preference requested for the element on which it is set. When a non-null preference is
    /// applied, the corresponding theme resources are injected into that element's resource scope. Descendants then
    /// resolve those resources through normal WPF resource lookup.
    /// </para>
    /// <para>
    /// The default value is <c>null</c>, which means that the element has no local theme request and inherits theme
    /// resources from its ancestors, application, or WPF theme dictionaries. Clearing the property after a local
    /// request removes the injected dictionary and restores that normal lookup behavior; an enum sentinel is not
    /// required to represent the dependency property's absence of a value.
    /// </para>
    /// <para>
    /// This setting is local in scope and does not modify the global <see cref="ThemeManager.Preference"/>. The final
    /// applied theme may still be affected by system policies such as high contrast mode. When set to
    /// <see cref="ThemePreference.System"/>, the request is resolved to either Light or Dark from the current system
    /// setting.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code lang="xml">
    /// &lt;Border ui:Theme.Requested="Dark" /&gt;
    /// </code>
    /// </example>
    public static readonly DependencyProperty RequestedProperty =
        DependencyProperty.RegisterAttached(
            "Requested",
            typeof(ThemePreference?),
            typeof(Theme),
            new FrameworkPropertyMetadata(
                null,
                static (d, e) => ThemeManager.OnRequestedThemeChanged(d, e)
            ),
            ValidateRequested
        );

    // Nullable<T> values are boxed as either null or the underlying enum value. Validate both shapes here so an
    // invalid cast cannot enter ThemeManager and silently select an unrelated palette during resource resolution.
    private static bool ValidateRequested(object? value) =>
        value is null
            or ThemePreference.Light
            or ThemePreference.Dark
            or ThemePreference.System;

    /// <summary>
    /// Helper for getting <see cref="RequestedProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="RequestedProperty"/> from.
    /// </param>
    /// <returns>
    /// The locally effective theme request, or <c>null</c> when the element has no request and uses normal resource
    /// lookup.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static ThemePreference? GetRequested(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (ThemePreference?)element.GetValue(RequestedProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="RequestedProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="RequestedProperty"/> on.
    /// </param>
    /// <param name="value">
    /// The requested theme preference, or <c>null</c> to clear the local request and restore normal resource lookup.
    /// </param>
    public static void SetRequested(DependencyObject element, ThemePreference? value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (value.HasValue)
        {
            element.SetCurrentValue(RequestedProperty, value.Value);
        }
        else
        {
            element.ClearValue(RequestedProperty);
        }
    }

    /// <summary>
    /// Clears the local <see cref="RequestedProperty"/> value so the element resumes resolving theme resources from
    /// its ancestors, application, or WPF theme dictionaries.
    /// </summary>
    /// <remarks>
    /// Clearing the dependency property is intentionally different from requesting <see cref="ThemePreference.Light"/>.
    /// The former removes the element-local theme dictionary; the latter installs an explicit Light dictionary that
    /// remains independent of the global preference.
    /// </remarks>
    /// <param name="element">
    /// <see cref="DependencyObject"/> from which to clear <see cref="RequestedProperty"/>.
    /// </param>
    public static void ClearRequested(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.ClearValue(RequestedProperty);
    }
}
