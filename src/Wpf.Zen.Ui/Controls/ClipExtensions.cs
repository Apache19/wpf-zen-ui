// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines the origin edge from which a clip reveal animation expands.
/// </summary>
public enum ClipDirection
{
    /// <summary>
    /// The clip expands from the left edge toward the right (default).
    /// </summary>
    LeftToRight = 0,

    /// <summary>
    /// The clip expands from the right edge toward the left.
    /// </summary>
    RightToLeft = 1,

    /// <summary>
    /// The clip expands from the top edge toward the bottom.
    /// </summary>
    TopToBottom = 2,

    /// <summary>
    /// The clip expands from the bottom edge toward the top.
    /// </summary>
    BottomToTop = 3,
}

#pragma warning disable WPF0061 // Accessor method for attached property should have standard documentation text

/// <summary>
/// Provides attached properties for percentage-based <see cref="UIElement.Clip"/> management,
/// enabling scroll-reveal animations without the need for value converters.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ClipPercentageProperty"/> as the animation target instead of
/// abusing <see cref="FrameworkElement.Tag"/> with a multi-binding converter.
/// </para>
/// <para>
/// On first use a single <see cref="System.Windows.Media.RectangleGeometry"/> is created and assigned
/// to <see cref="UIElement.Clip"/>. Subsequent frames only mutate its <see cref="System.Windows.Media.RectangleGeometry.Rect"/>
/// property — a value-type update with zero heap allocation and no Freezable churn.
/// The geometry is intentionally left unfrozen (it must remain mutable for animation)
/// but is held for the lifetime of the element, so it does not cause the memory-leak
/// pattern associated with transient unfrozen Freezables.
/// </para>
/// </remarks>
public static class ClipExtensions
{
    /// <summary>
    /// Identifies the <c>ClipPercentage</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty ClipPercentageProperty = DependencyProperty.RegisterAttached(
        "ClipPercentage",
        typeof(double),
        typeof(ClipExtensions),
        new PropertyMetadata(0.0, OnClipPercentageChanged)
    );

    /// <summary>
    /// Identifies the <c>ObserveSizeChanges</c> attached dependency property.
    /// When set to <see langword="true"/>, hooks the <see cref="FrameworkElement.SizeChanged"/>
    /// event so the clip geometry stays in sync with the element's render size.
    /// </summary>
    public static readonly DependencyProperty ObserveSizeChangesProperty = DependencyProperty.RegisterAttached(
        "ObserveSizeChanges",
        typeof(bool),
        typeof(ClipExtensions),
        new PropertyMetadata(false, OnObserveSizeChangesChanged)
    );

    /// <summary>
    /// Identifies the <c>Direction</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.RegisterAttached(
        "Direction",
        typeof(ClipDirection),
        typeof(ClipExtensions),
        new PropertyMetadata(ClipDirection.LeftToRight, OnDirectionChanged)
    );

    /// <summary>
    /// Gets the fraction of the element's width that is visible through the clip region,
    /// used to drive scroll-reveal animations.
    /// </summary>
    /// <param name="element">The target <see cref="UIElement"/>.</param>
    /// <returns>
    /// A value between <c>0.0</c> (fully clipped — nothing visible) and <c>1.0</c>
    /// (fully visible). Values outside this range are clamped during geometry creation.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(UIElement))]
    public static double GetClipPercentage(UIElement element)
    {
        return (double)element.GetValue(ClipPercentageProperty);
    }

    /// <summary>
    /// Sets the fraction of the element's width that is visible through the clip region.
    /// Animate this property (e.g. <c>0.0</c> → <c>1.0</c>) to create a left-to-right reveal effect.
    /// </summary>
    /// <param name="element">The target <see cref="UIElement"/>.</param>
    /// <param name="value">
    /// The clip fraction, clamped to [0, 1] when the geometry is generated.
    /// </param>
    public static void SetClipPercentage(UIElement element, double value)
    {
        element.SetValue(ClipPercentageProperty, value);
    }

    /// <summary>
    /// Gets whether the element automatically updates its clip geometry when its
    /// <see cref="FrameworkElement.SizeChanged"/> event fires (e.g. on DPI or layout changes).
    /// </summary>
    /// <param name="element">The target <see cref="FrameworkElement"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the element keeps its clip in sync with
    /// <see cref="UIElement.RenderSize"/>; otherwise <see langword="false"/>.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static bool GetObserveSizeChanges(FrameworkElement element)
    {
        return (bool)element.GetValue(ObserveSizeChangesProperty);
    }

    /// <summary>
    /// Sets whether the element automatically updates its clip geometry to match
    /// its <see cref="UIElement.RenderSize"/> when resized.
    /// Enable this when the element's size can change after initial layout
    /// (e.g. inside a resizable container or under DPI scaling).
    /// </summary>
    /// <param name="element">The target <see cref="FrameworkElement"/>.</param>
    /// <param name="value">
    /// <see langword="true"/> to subscribe to <see cref="FrameworkElement.SizeChanged"/>;
    /// <see langword="false"/> to unsubscribe.
    /// </param>
    public static void SetObserveSizeChanges(FrameworkElement element, bool value)
    {
        element.SetValue(ObserveSizeChangesProperty, value);
    }

    /// <summary>
    /// Gets the origin edge from which the clip reveal animation expands.
    /// </summary>
    /// <param name="element">The target <see cref="UIElement"/>.</param>
    /// <returns>
    /// The <see cref="ClipDirection"/> value that controls which edge the clip rectangle
    /// expands from as <see cref="ClipPercentageProperty"/> increases from <c>0.0</c> to <c>1.0</c>.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(UIElement))]
    public static ClipDirection GetDirection(UIElement element)
    {
        return (ClipDirection)element.GetValue(DirectionProperty);
    }

    /// <summary>
    /// Sets the origin edge from which the clip reveal animation expands.
    /// </summary>
    /// <param name="element">The target <see cref="UIElement"/>.</param>
    /// <param name="value">
    /// The <see cref="ClipDirection"/> value. Default is <see cref="ClipDirection.LeftToRight"/>.
    /// </param>
    public static void SetDirection(UIElement element, ClipDirection value)
    {
        element.SetValue(DirectionProperty, value);
    }

    // ============================================================
    // Property changed callbacks
    // ============================================================

    private static void OnClipPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        UpdateClip(element);
    }

    private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        UpdateClip(element);
    }

    private static void OnObserveSizeChangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.SizeChanged += OnElementSizeChanged;
        }
        else
        {
            element.SizeChanged -= OnElementSizeChanged;
        }
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        UpdateClip(element);
    }

    // ============================================================
    // Core logic
    // ============================================================

    /// <summary>
    /// Ensures the element's <see cref="UIElement.Clip"/> is a <see cref="RectangleGeometry"/>
    /// whose <see cref="RectangleGeometry.Rect"/> reflects the current render size,
    /// <see cref="ClipPercentageProperty"/>, and <see cref="DirectionProperty"/>.
    /// </summary>
    /// <remarks>
    /// On the first call a single <see cref="RectangleGeometry"/> is created and
    /// assigned to <c>Clip</c>. Every subsequent call mutates only the existing geometry's
    /// <c>Rect</c> property — a <see cref="Rect"/> is a value type, so this
    /// produces zero heap allocation per frame.
    /// <para>
    /// If external code replaces <c>Clip</c> with a non-<c>RectangleGeometry</c>, the next
    /// invocation transparently creates a fresh instance.
    /// </para>
    /// </remarks>
    /// <param name="element">The element whose <c>Clip</c> will be updated.</param>
    private static void UpdateClip(UIElement element)
    {
        var size = element.RenderSize;

        if (size.IsEmpty || double.IsNaN(size.Width) || double.IsNaN(size.Height))
        {
            return;
        }

        // Clamp percentage to a sensible range to avoid negative or oversized geometry.
        var percentage = GetClipPercentage(element);
        var clamped = percentage < 0.0 ? 0.0 : percentage > 1.0 ? 1.0 : percentage;

        var direction = GetDirection(element);
        var newRect = CreateClipRect(size, clamped, direction);

        if (element.Clip is RectangleGeometry existingGeometry)
        {
            // Fast path: mutate the existing geometry's Rect in-place.
            // Rect is a DependencyProperty on RectangleGeometry, so WPF picks up
            // the change automatically and re-renders the clipped region.
            existingGeometry.Rect = newRect;
        }
        else
        {
            // First call (or Clip was replaced by external code): create the single
            // long-lived geometry. Intentionally NOT frozen — it must remain mutable
            // so subsequent frames can update Rect without allocating.
            var geometry = new RectangleGeometry(newRect);
            element.SetCurrentValue(UIElement.ClipProperty, geometry);
        }
    }

    /// <summary>
    /// Computes the clip <see cref="Rect"/> for the given render size, percentage, and direction.
    /// </summary>
    private static Rect CreateClipRect(Size size, double percentage, ClipDirection direction)
    {
        return direction switch
        {
            ClipDirection.LeftToRight => new Rect(0, 0, size.Width * percentage, size.Height),
            ClipDirection.RightToLeft => new Rect(size.Width - (size.Width * percentage), 0, size.Width * percentage, size.Height),
            ClipDirection.TopToBottom => new Rect(0, 0, size.Width, size.Height * percentage),
            ClipDirection.BottomToTop => new Rect(0, size.Height - (size.Height * percentage), size.Width, size.Height * percentage),
            _ => new Rect(0, 0, size.Width * percentage, size.Height),
        };
    }
}
#pragma warning restore WPF0061 // Accessor method for attached property should have standard documentation text
