// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents the keyboard-focus outline for the element owned by WPF's focus adorner.
/// </summary>
/// <remarks>
/// <para>
/// WPF creates an internal adorner and applies <see cref="SystemParameters.FocusVisualStyleKey"/> to a
/// <see cref="Control"/> hosted by that adorner. This presenter is placed in the control template and resolves the
/// adorned element after the visual tree has been connected. Keeping target discovery here avoids XAML paths such
/// as <c>AdornedElement.CornerRadius</c>, which report a binding error for controls that do not expose that property.
/// </para>
/// <para>
/// The presenter resolves a rounded-rectangle outline from <see cref="FocusVisual.CornerRadiusProperty"/>, a public
/// dependency property named <c>CornerRadius</c> on the adorned element, or an explicitly configured
/// <see cref="Border.CornerRadiusProperty"/>, in that order. Controls with a non-rectangular outline should provide a
/// <see cref="FocusVisual.PresenterStyleProperty"/> whose template draws the required geometry. Arbitrary control
/// templates are intentionally not inspected because a visual tree does not expose a reliable semantic outline.
/// </para>
/// <para>
/// The control has no required template parts. The library's default style uses a <see cref="Border"/> bound to
/// <see cref="CornerRadius"/>; a custom presenter style may replace that template completely and omit the border.
/// </para>
/// </remarks>
public class FocusVisualPresenter : Control
{
    private static readonly object TargetMetadataCacheLock = new();
    private static readonly Dictionary<Type, TargetMetadata> TargetMetadataCache = new();

    private UIElement?      _subscribedElement;
    private TargetMetadata? _targetMetadata;
    private Style?          _defaultPresenterStyle;
    private bool            _hasCapturedDefaultPresenterStyle;

    static FocusVisualPresenter()
    {
        FocusableProperty.OverrideMetadata(
            typeof(FocusVisualPresenter),
            new FrameworkPropertyMetadata(false)
        );

        IsHitTestVisibleProperty.OverrideMetadata(
            typeof(FocusVisualPresenter),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FocusVisualPresenter"/> class.
    /// </summary>
    public FocusVisualPresenter()
    {
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static readonly DependencyPropertyKey AdornedElementPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(AdornedElement),
            typeof(UIElement),
            typeof(FocusVisualPresenter),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="AdornedElement"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AdornedElementProperty = AdornedElementPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the element whose keyboard-focus outline is being presented.
    /// </summary>
    /// <remarks>
    /// The value is populated after the presenter is connected beneath WPF's focus adorner and becomes
    /// <see langword="null"/> when it is disconnected. Custom presenter templates may bind to this property when
    /// they require target state beyond the resolved <see cref="CornerRadius"/>.
    /// </remarks>
    public UIElement? AdornedElement => (UIElement?)GetValue(AdornedElementProperty);

    private static readonly DependencyPropertyKey CornerRadiusPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(FocusVisualPresenter),
            new FrameworkPropertyMetadata(new CornerRadius(4))
        );

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty = CornerRadiusPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the corner radius resolved for the current <see cref="AdornedElement"/>.
    /// </summary>
    /// <remarks>
    /// The value updates while the focus visual is visible when the explicit, conventional, or attached radius
    /// source changes. Custom presenter templates should consume this property instead of binding directly to a
    /// property on <see cref="AdornedElement"/>.
    /// </remarks>
    public CornerRadius CornerRadius => (CornerRadius)GetValue(CornerRadiusProperty);

    /// <summary>
    /// Identifies the <see cref="FallbackCornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FallbackCornerRadiusProperty = DependencyProperty.Register(
        nameof(FallbackCornerRadius),
        typeof(CornerRadius),
        typeof(FocusVisualPresenter),
        new FrameworkPropertyMetadata(new CornerRadius(4), OnFallbackCornerRadiusChanged),
        ValidateFallbackCornerRadius
    );

    /// <summary>
    /// Gets or sets the corner radius used when the adorned element does not describe a rounded-rectangle outline.
    /// The default is <c>4,4,4,4</c>.
    /// </summary>
    /// <remarks>
    /// This property affects the presenter's template output but does not affect layout. It is ignored whenever an
    /// explicit or conventional corner-radius source is available on <see cref="AdornedElement"/>.
    /// </remarks>
    public CornerRadius FallbackCornerRadius
    {
        get => (CornerRadius)GetValue(FallbackCornerRadiusProperty);
        set => SetValue(FallbackCornerRadiusProperty, value);
    }

    /// <summary>
    /// Connects the presenter to an adorned element and synchronizes its style and rounded-rectangle geometry.
    /// </summary>
    /// <param name="element">The element being adorned, or <see langword="null"/> to disconnect the presenter.</param>
    /// <remarks>
    /// This method is internal so tests can exercise the same synchronization path without constructing WPF's
    /// private focus-adorner implementation. Production callers rely on the presenter's loaded lifecycle instead.
    /// </remarks>
    internal void SetAdornedElement(UIElement? element)
    {
        CaptureDefaultPresenterStyle();

        if (ReferenceEquals(_subscribedElement, element))
        {
            UpdateFromAdornedElement();
            return;
        }

        UnsubscribeFromAdornedElement();

        _subscribedElement = element;
        _targetMetadata    = element is null ? null : GetTargetMetadata(element.GetType());

        SetValue(AdornedElementPropertyKey, element);

        SubscribeToAdornedElement();
        UpdateFromAdornedElement();
    }

    private static bool ValidateFallbackCornerRadius(object? value)
    {
        return value is CornerRadius cornerRadius && Border.CornerRadiusProperty.IsValidValue(cornerRadius);
    }

    private static void OnFallbackCornerRadiusChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        ((FocusVisualPresenter)dependencyObject).UpdateCornerRadius();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        CaptureDefaultPresenterStyle();
        SetAdornedElement(FindAdornedElement());
    }

    private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        SetAdornedElement(null);
    }

    private UIElement? FindAdornedElement()
    {
        // The internal WPF FocusVisualAdorner is deliberately private, but its public Adorner base exposes the
        // target. Searching after Loaded is important: the template child can be created before the outer focus
        // control itself has been inserted into the adorner's visual tree.
        for (DependencyObject? ancestor = VisualTreeHelper.GetParent(this);
             ancestor is not null;
             ancestor = VisualTreeHelper.GetParent(ancestor))
        {
            if (ancestor is Adorner adorner)
            {
                return adorner.AdornedElement;
            }
        }

        return null;
    }

    private void CaptureDefaultPresenterStyle()
    {
        if (_hasCapturedDefaultPresenterStyle)
        {
            return;
        }

        // GlobalResources supplies the default style directly on the presenter. Capturing that value before a
        // target-specific style is applied lets a live presenter return to the default when a style trigger clears
        // FocusVisual.PresenterStyle.
        _defaultPresenterStyle = Style;
        _hasCapturedDefaultPresenterStyle = true;
    }

    private void SubscribeToAdornedElement()
    {
        if (_subscribedElement is null || _targetMetadata is null)
        {
            return;
        }

        foreach (var descriptor in _targetMetadata.ValueDescriptors)
        {
            descriptor.AddValueChanged(_subscribedElement, OnAdornedElementValueChanged);
        }
    }

    private void UnsubscribeFromAdornedElement()
    {
        if (_subscribedElement is null || _targetMetadata is null)
        {
            return;
        }

        foreach (var descriptor in _targetMetadata.ValueDescriptors)
        {
            descriptor.RemoveValueChanged(_subscribedElement, OnAdornedElementValueChanged);
        }
    }

    private void OnAdornedElementValueChanged(object? sender, EventArgs eventArgs)
    {
        UpdateFromAdornedElement();
    }

    private void UpdateFromAdornedElement()
    {
        UpdateCornerRadius();
        UpdatePresenterStyle();
    }

    private void UpdateCornerRadius()
    {
        var cornerRadius = ResolveCornerRadius();
        if (CornerRadius != cornerRadius)
        {
            SetValue(CornerRadiusPropertyKey, cornerRadius);
        }
    }

    private CornerRadius ResolveCornerRadius()
    {
        if (_subscribedElement is null || _targetMetadata is null)
        {
            return FallbackCornerRadius;
        }

        if (FocusVisual.GetCornerRadius(_subscribedElement) is { } explicitCornerRadius)
        {
            return explicitCornerRadius;
        }

        if (
            TryGetConfiguredCornerRadius(
                _subscribedElement,
                _targetMetadata.ConventionalCornerRadiusProperty,
                out var cornerRadius
            )
        )
        {
            return cornerRadius;
        }

        if (
            _targetMetadata.ConventionalCornerRadiusProperty != Border.CornerRadiusProperty
            && TryGetConfiguredCornerRadius(
                _subscribedElement,
                Border.CornerRadiusProperty,
                out cornerRadius
            )
        )
        {
            return cornerRadius;
        }

        return FallbackCornerRadius;
    }

    private void UpdatePresenterStyle()
    {
        var requestedStyle = _subscribedElement is null ? null : FocusVisual.GetPresenterStyle(_subscribedElement);
        var presenterStyle = IsStyleApplicable(requestedStyle) ? requestedStyle : _defaultPresenterStyle;

        if (ReferenceEquals(Style, presenterStyle))
        {
            return;
        }

        if (presenterStyle is null)
        {
            ClearValue(StyleProperty);
        }
        else
        {
            SetValue(StyleProperty, presenterStyle);
        }
    }

    private bool IsStyleApplicable(Style? style)
    {
        // An incompatible Style normally throws when assigned. A malformed focus-style customization must not turn
        // keyboard navigation into an application-ending failure, so the presenter degrades to its captured default.
        return style?.TargetType?.IsInstanceOfType(this) == true;
    }

    private static bool TryGetConfiguredCornerRadius(
        DependencyObject element,
        DependencyProperty? property,
        out CornerRadius cornerRadius)
    {
        if (property is null || element.GetValue(property) is not CornerRadius candidate)
        {
            cornerRadius = default;
            return false;
        }

        // A non-zero metadata default is a meaningful shape. Zero is ambiguous, so its value source distinguishes an
        // untouched dependency-property default from an explicitly square corner supplied by a style or local value.
        var valueSource = DependencyPropertyHelper.GetValueSource(element, property);
        if (candidate != default || valueSource.BaseValueSource != BaseValueSource.Default)
        {
            cornerRadius = candidate;
            return true;
        }

        cornerRadius = default;
        return false;
    }

    private static TargetMetadata GetTargetMetadata(Type targetType)
    {
        lock (TargetMetadataCacheLock)
        {
            if (TargetMetadataCache.TryGetValue(targetType, out var metadata))
            {
                return metadata;
            }

            metadata = CreateTargetMetadata(targetType);
            TargetMetadataCache.Add(targetType, metadata);
            return metadata;
        }
    }

    private static TargetMetadata CreateTargetMetadata(Type targetType)
    {
        // DependencyPropertyDescriptor resolves a public dependency property registered by the target type or one of
        // its bases without reflection. The result is cached because focus navigation repeatedly visits the same small
        // set of control types and descriptor creation is comparatively expensive.
        var conventionalDescriptor = DependencyPropertyDescriptor.FromName(
            nameof(Border.CornerRadius),
            targetType,
            targetType
        );

        var explicitDescriptor = DependencyPropertyDescriptor.FromProperty(
            FocusVisual.CornerRadiusProperty,
            targetType
        );

        var presenterStyleDescriptor = DependencyPropertyDescriptor.FromProperty(
            FocusVisual.PresenterStyleProperty,
            targetType
        );

        var borderDescriptor = DependencyPropertyDescriptor.FromProperty(
            Border.CornerRadiusProperty,
            targetType
        );

        var descriptors = new List<DependencyPropertyDescriptor>(4);

        AddDescriptor(descriptors, explicitDescriptor);
        AddDescriptor(descriptors, presenterStyleDescriptor);
        AddDescriptor(descriptors, conventionalDescriptor);
        AddDescriptor(descriptors, borderDescriptor);

        return new TargetMetadata(conventionalDescriptor?.DependencyProperty, descriptors.ToArray());
    }

    private static void AddDescriptor(
        ICollection<DependencyPropertyDescriptor> descriptors,
        DependencyPropertyDescriptor? descriptor)
    {
        if (descriptor is not null
            && descriptors.All(candidate => candidate.DependencyProperty != descriptor.DependencyProperty))
        {
            descriptors.Add(descriptor);
        }
    }

    private sealed class TargetMetadata
    {
        public TargetMetadata(
            DependencyProperty? conventionalCornerRadiusProperty,
            DependencyPropertyDescriptor[] valueDescriptors)
        {
            ConventionalCornerRadiusProperty = conventionalCornerRadiusProperty;
            ValueDescriptors = valueDescriptors;
        }

        public DependencyProperty? ConventionalCornerRadiusProperty { get; }

        public DependencyPropertyDescriptor[] ValueDescriptors { get; }
    }
}