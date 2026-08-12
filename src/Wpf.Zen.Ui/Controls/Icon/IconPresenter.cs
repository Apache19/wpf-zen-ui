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
    /// <summary>Identifies the <see cref="IconSize"/> dependency property.</summary>
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
        validateValueCallback: value =>
            double.IsNaN((double)value) || TextElement.FontSizeProperty.IsValidValue((double)value));

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

    /// <summary>
    /// Helper for getting <see cref="IconSizeProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IconSizeProperty"/> from.
    /// </param>
    /// <returns>IconSize property value.</returns>
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
    /// Helper for setting <see cref="IconSizeProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IconSizeProperty"/> on.
    /// </param>
    /// <param name="value">IconSize property value.</param>
    public static void SetIconSize(DependencyObject element, double value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconWeight"/> dependency property.
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

    /// <summary>
    /// Helper for getting <see cref="IconWeightProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IconWeightProperty"/> from.
    /// </param>
    /// <returns>IconWeight property value.</returns>
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
    /// Helper for setting <see cref="IconWeightProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IconWeightProperty"/> on.
    /// </param>
    /// <param name="value">IconWeight property value.</param>
    public static void SetIconWeight(DependencyObject element, FontWeight? value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IconWeightProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconForeground"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The property carries an icon-specific foreground brush through a presentation subtree
    /// without changing the foreground of ordinary text. It intentionally uses a
    /// <see cref="Brush"/> rather than a color so callers retain WPF foreground capabilities such
    /// as theme resources, gradients, opacity, and animation. A <see langword="null"/> value means
    /// that compatible icon families should preserve their native foreground behavior.
    /// </remarks>
    public static readonly DependencyProperty IconForegroundProperty = DependencyProperty.RegisterAttached(
        nameof(IconForeground),
        typeof(Brush),
        typeof(IconPresenter),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>
    /// Gets or sets the foreground brush requested for compatible icon content.
    /// </summary>
    /// <remarks>
    /// The default <see langword="null"/> value leaves each icon's native foreground unchanged. A
    /// concrete brush is icon-specific presentation context and does not affect ordinary text.
    /// </remarks>
    public Brush? IconForeground
    {
        get => GetIconForeground(this);
        set => SetIconForeground(this, value);
    }

    /// <summary>
    /// Helper for getting <see cref="IconForegroundProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IconForegroundProperty"/> from.
    /// </param>
    /// <returns>IconForeground property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static Brush? GetIconForeground(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (Brush?)element.GetValue(IconForegroundProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="IconForegroundProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IconForegroundProperty"/> on.
    /// </param>
    /// <param name="value">IconForeground property value.</param>
    public static void SetIconForeground(DependencyObject element, Brush? value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(IconForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Variant"/> dependency property.
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

    /// <summary>
    /// Helper for getting <see cref="VariantProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="VariantProperty"/> from.
    /// </param>
    /// <returns>Variant property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static IconVariant GetVariant(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (IconVariant)element.GetValue(VariantProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="VariantProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="VariantProperty"/> on.
    /// </param>
    /// <param name="value">Variant property value.</param>
    public static void SetVariant(DependencyObject element, IconVariant value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(VariantProperty, value);
    }
}
