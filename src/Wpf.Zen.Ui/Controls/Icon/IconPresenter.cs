// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents icon content and supplies presentation context understood by variant-aware icons.
/// </summary>
/// <remarks>
/// <para>
/// This presenter deliberately derives from WPF's native <see cref="ContentPresenter"/>. It
/// therefore preserves implicit data templates, explicit templates, selectors, arbitrary visual
/// content, text-property inheritance, and the usual content-template lookup order.
/// </para>
/// <para>
/// <see cref="VariantProperty"/> inherits only through the presenter's content subtree. A control
/// template can consequently request a filled active icon by setting one property on this
/// presenter; it does not need to locate a family-specific visual, replace an application binding,
/// or know which icon source produced the visual. A custom icon family participates by adding the
/// dependency property as an owner on its visual element and interpreting the value it receives.
/// </para>
/// </remarks>
public class IconPresenter : ContentPresenter
{
    /// <summary>
    /// Identifies the presentation variant propagated through an icon content subtree.
    /// </summary>
    /// <remarks>
    /// The property is registered as attached as well as exposed by the presenter so a detached
    /// WPF presentation boundary, such as a <see cref="MenuItem"/> hosted in a
    /// <see cref="System.Windows.Controls.Primitives.Popup"/>,
    /// can copy the context onto its own root. Ordinary control templates should set
    /// <see cref="Variant"/> directly on their named presenter.
    /// </remarks>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.RegisterAttached(
        nameof(Variant),
        typeof(IconVariant),
        typeof(IconPresenter),
        new FrameworkPropertyMetadata(
            IconVariant.Default,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>
    /// Gets or sets the variant requested for icon content displayed by this presenter.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="IconVariant.Default"/>, which leaves the source's own
    /// selection unchanged.
    /// </remarks>
    public IconVariant Variant
    {
        get => GetVariant(this);
        set => SetVariant(this, value);
    }

    /// <summary>Gets the icon variant inherited by <paramref name="element"/>.</summary>
    /// <param name="element">The presentation root or icon element to inspect.</param>
    /// <returns>The effective icon variant.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static IconVariant GetVariant(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (IconVariant)element.GetValue(VariantProperty);
    }

    /// <summary>Sets the icon variant inherited by descendants of <paramref name="element"/>.</summary>
    /// <param name="element">The presentation root or icon presenter to configure.</param>
    /// <param name="value">The variant requested from compatible icon families.</param>
    public static void SetVariant(DependencyObject element, IconVariant value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(VariantProperty, value);
    }
}
