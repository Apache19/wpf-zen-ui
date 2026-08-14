// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects the built-in presentation for <see cref="NavigationMapNode"/> values while preserving
/// WPF's normal fallback presentation for directly supplied visual items and other data types.
/// </summary>
/// <remarks>
/// <see cref="NavigationMapView"/> uses this selector in its default style instead of assigning an
/// <see cref="ItemsControl.ItemTemplate"/>. Consequently, an application can override either
/// <see cref="ItemsControl.ItemTemplate"/> or <see cref="ItemsControl.ItemTemplateSelector"/> and
/// receive standard WPF precedence without control-specific flags or template detection.
/// </remarks>
public sealed class NavigationMapNodeTemplateSelector : DataTemplateSelector
{
    /// <summary>Gets or sets the template applied to <see cref="NavigationMapNode"/> values.</summary>
    public DataTemplate? NodeTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is NavigationMapNode ? NodeTemplate : base.SelectTemplate(item, container);
}
