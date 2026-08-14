// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects the built-in, wrapping presentation for textual navigation-map content.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="NavigationMapView"/> style uses this selector for
/// <see cref="NavigationMapNode.Content"/>. Strings receive <see cref="TextTemplate"/> so localized
/// labels can wrap within a fixed-width node. Other values deliberately return
/// <see langword="null"/>, which asks <see cref="ContentPresenter"/> to continue through WPF's
/// ordinary implicit data-template and UI-element fallback rules.
/// </para>
/// <para>
/// Keeping text specialization at the template-selection boundary preserves the full object-valued
/// content contract; converting every value with <see cref="object.ToString"/> would prevent
/// applications from presenting icons, view models, or custom visual content by type.
/// </para>
/// </remarks>
public sealed class NavigationMapContentTemplateSelector : DataTemplateSelector
{
    /// <summary>Gets or sets the template applied when the content value is a string.</summary>
    /// <remarks>
    /// When this property is <see langword="null"/>, strings also continue through WPF's default
    /// content-template selection.
    /// </remarks>
    public DataTemplate? TextTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is string ? TextTemplate : base.SelectTemplate(item, container);
}
