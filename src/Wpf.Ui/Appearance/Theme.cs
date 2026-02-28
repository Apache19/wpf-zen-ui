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
    /// <summary>
    /// Identifies the ApplyToRootVisual attached property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is used to register the RootVisual element with ThemeManager,
    /// allowing it to respond to global theme changes.
    /// </para>
    /// <para>
    /// This is specifically designed for non-WPF hosting environments (e.g., WinForms ElementHost)
    /// where WPF content does not have a global Application object.
    /// When set to true on the root UserControl, the RootVisual will be registered
    /// and will automatically update when <see cref="ThemeManager.ThemeMode"/> changes.
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
                UiThemeMode.Auto,
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