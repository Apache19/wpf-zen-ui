// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library */

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents an item in a <see cref="BreadcrumbBar"/> control.
/// </summary>
public class BreadcrumbBarItem : System.Windows.Controls.Primitives.ButtonBase
{
    /// <summary>Identifies the <see cref="IsLast"/> dependency property.</summary>
    private static readonly DependencyPropertyKey IsLastPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsLast),
            typeof(bool),
            typeof(BreadcrumbBarItem),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Gets a value indicating whether the current item is the last one.
    /// </summary>
    /// <remarks>
    /// The owning <see cref="BreadcrumbBar"/> maintains this value as containers are generated or
    /// cleared. It is read-only because callers cannot change an item's structural position.
    /// </remarks>
    public bool IsLast => (bool)GetValue(IsLastProperty);

    /// <summary>Updates the position state owned by the parent item generator.</summary>
    internal void SetIsLast(bool value) => SetValue(IsLastPropertyKey, value);

    /// <summary>Identifies the <see cref="IsLast"/> dependency property.</summary>
    public static readonly DependencyProperty IsLastProperty = IsLastPropertyKey.DependencyProperty;

    static BreadcrumbBarItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(BreadcrumbBarItem),
            new FrameworkPropertyMetadata(typeof(BreadcrumbBarItem))
        );
    }
}
