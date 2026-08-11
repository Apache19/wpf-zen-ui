// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;

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
    /// Identifies the attached <c>IconSize</c> dependency property.
    /// </summary>
    /// <remarks>
    /// The property is a presentation-scoped counterpart to WPF's inherited text size. Setting it
    /// on a control, panel, or presenter affects compatible icon elements in that subtree without
    /// changing the size of ordinary text. <see cref="double.NaN"/> means that no icon-specific
    /// size was requested, allowing each icon family to preserve its native sizing behavior.
    /// </remarks>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.RegisterAttached(
        nameof(IconSize),
        typeof(double),
        typeof(IconPresenter),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.Inherits
        ),
        ValidateIconSize
    );

    /// <summary>
    /// Gets or sets the logical size requested for compatible icon content.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="double.NaN"/>, which leaves the icon's native size
    /// unchanged. A concrete value is semantic presentation context and therefore takes precedence
    /// over a compatible icon's low-level font size while it is in scope.
    /// </remarks>
    public double IconSize
    {
        get => GetIconSize(this);
        set => SetIconSize(this, value);
    }

    /// <summary>Gets the icon size inherited by <paramref name="element"/>.</summary>
    /// <param name="element">The presentation root or icon element to inspect.</param>
    /// <returns>
    /// The effective icon size, or <see cref="double.NaN"/> when native icon sizing is requested.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static double GetIconSize(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (double)element.GetValue(IconSizeProperty);
    }

    /// <summary>
    /// Sets the icon size inherited by compatible icon descendants of <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The presentation root or icon presenter to configure.</param>
    /// <param name="value">
    /// The requested size, or <see cref="double.NaN"/> to restore native icon sizing.
    /// </param>
    public static void SetIconSize(DependencyObject element, double value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Identifies the attached <c>IconWeight</c> dependency property.
    /// </summary>
    /// <remarks>
    /// The property describes icon stroke or font weight independently of ordinary text weight.
    /// Compatible font-backed icons can map it to <see cref="TextElement.FontWeight"/>, while other
    /// icon families can interpret the same semantic value using their own rendering mechanism.
    /// </remarks>
    public static readonly DependencyProperty IconWeightProperty = DependencyProperty.RegisterAttached(
        nameof(IconWeight),
        typeof(FontWeight?),
        typeof(IconPresenter),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>
    /// Gets or sets the logical weight requested for compatible icon content.
    /// </summary>
    /// <remarks>
    /// The default <see langword="null"/> value leaves each icon's native weight unchanged. A
    /// concrete value is icon-specific presentation context and does not affect ordinary text.
    /// </remarks>
    public FontWeight? IconWeight
    {
        get => GetIconWeight(this);
        set => SetIconWeight(this, value);
    }

    /// <summary>Gets the icon weight inherited by <paramref name="element"/>.</summary>
    /// <param name="element">The presentation root or icon element to inspect.</param>
    /// <returns>The effective icon weight, or <see langword="null"/> for native icon weighting.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static FontWeight? GetIconWeight(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (FontWeight?)element.GetValue(IconWeightProperty);
    }

    /// <summary>
    /// Sets the icon weight inherited by compatible icon descendants of <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The presentation root or icon presenter to configure.</param>
    /// <param name="value">The requested weight, or <see langword="null"/> for native weighting.</param>
    public static void SetIconWeight(DependencyObject element, FontWeight? value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IconWeightProperty, value);
    }

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

    private static bool ValidateIconSize(object? value)
    {
        double size = (double)value!;

        // Reuse WPF's own FontSize validation so font-backed icon families cannot inherit a value
        // that their rendering property would reject. NaN is reserved as this contract's explicit
        // "not specified" sentinel and is never forwarded to FontSize.
        return double.IsNaN(size) || TextElement.FontSizeProperty.IsValidValue(size);
    }
}
