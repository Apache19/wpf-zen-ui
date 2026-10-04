// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a font icon inline, participating in text wrapping and baseline layout.
/// </summary>
/// <remarks>
/// Source owns glyph and font selection. FontSize, FontWeight and Foreground follow normal
/// text inheritance. The baseline defaults to Center; Variant follows IconPresenter's scope.
/// </remarks>
public class FontIconSpan : Run
{
    static FontIconSpan()
    {
        BaselineAlignmentProperty.OverrideMetadata(
            typeof(FontIconSpan), new FrameworkPropertyMetadata(BaselineAlignment.Center));
        FontFamilyProperty.OverrideMetadata(
            typeof(FontIconSpan), new FrameworkPropertyMetadata(null, CoerceFontFamily));
    }

    private FontIconSource? _effectiveSource;

    private static readonly DependencyProperty ResourceFontFamilyProperty = DependencyProperty.Register(
        "ResourceFontFamily", typeof(FontFamily), typeof(FontIconSpan),
        new PropertyMetadata(null, OnResourceFontChanged));

    /// <summary>
    /// Identifies the <see cref="Source"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(FontIconSource), typeof(FontIconSpan),
        new FrameworkPropertyMetadata(null, OnSourceChanged));

    /// <summary>
    /// Gets or sets the immutable font icon description. Null displays no glyph.
    /// Replace this value to change the icon; one source may be shared by multiple hosts.
    /// </summary>
    public FontIconSource? Source
    {
        get => (FontIconSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Variant"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VariantProperty = IconPresenter.VariantProperty.AddOwner(
        typeof(FontIconSpan),
        new FrameworkPropertyMetadata(
            IconVariant.Default, FrameworkPropertyMetadataOptions.Inherits, OnSourceChanged));

    /// <summary>
    /// Gets or sets the inherited presentation request, interpreted by the source.
    /// Unsupported variants retain the source's own appearance. The default is Default.
    /// </summary>
    public IconVariant Variant
    {
        get => (IconVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Initializes an empty font icon. Set Source to select a family and glyph.
    /// </summary>
    public FontIconSpan()
    {
    }

    /// <summary>
    /// Initializes a font icon with a reusable description.
    /// </summary>
    /// <param name="source">The font icon description to display.</param>
    public FontIconSpan(FontIconSource source)
        : this()
    {
        Source = source;
    }

    /// <summary>
    /// Resolves a presentation without modifying the shared source or its binding.
    /// Resource expressions belong to this host so each host follows its own resource scope.
    /// </summary>
    private void UpdateSource()
    {
        _effectiveSource = Source?.WithVariant(Variant);
        string glyph = _effectiveSource?.Glyph ?? string.Empty;
        SetCurrentValue(TextProperty, glyph);

        object? resourceKey = _effectiveSource?.FontFamilyResourceKey;
        if (resourceKey is null)
        {
            ClearValue(ResourceFontFamilyProperty);
        }
        else
        {
            SetResourceReference(ResourceFontFamilyProperty, resourceKey);
        }

        CoerceValue(FontFamilyProperty);
    }

    /// <summary>
    /// Uses a scoped font when available and the source's font otherwise.
    /// Clearing Source restores the ordinary inherited font.
    /// </summary>
    private static object CoerceFontFamily(DependencyObject sender, object baseValue)
    {
        var icon = (FontIconSpan)sender;
        var resourceFont = (FontFamily?)icon.GetValue(ResourceFontFamilyProperty);
        return resourceFont ?? icon._effectiveSource?.FontFamily ?? baseValue;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (FontIconSpan)sender;
        icon.UpdateSource();
    }

    private static void OnResourceFontChanged(
        DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        sender.CoerceValue(FontFamilyProperty);
    }
}
