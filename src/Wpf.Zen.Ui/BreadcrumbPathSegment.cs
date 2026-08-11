// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Declares one segment of a page-level <see cref="BreadcrumbPath"/>.
/// </summary>
/// <remarks>
/// A segment with neither <see cref="PageType"/> nor <see cref="RouteKey"/> is structural and is
/// displayed without navigation behavior. Set <see cref="RouteKey"/> when clicking the segment
/// should resolve a map route, or set <see cref="PageType"/> for a page destination. All properties
/// are dependency properties and therefore support bindings in page XAML.
/// </remarks>
public sealed class BreadcrumbPathSegment : Freezable
{
    /// <summary>Identifies the <see cref="Key"/> dependency property.</summary>
    /// <remarks>The default is <see langword="null"/>. It has no direct layout or rendering effect.</remarks>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
        nameof(Key),
        typeof(string),
        typeof(BreadcrumbPathSegment),
        new PropertyMetadata(null),
        value => value is null || !string.IsNullOrWhiteSpace((string)value)
    );

    /// <summary>Identifies the <see cref="Content"/> dependency property.</summary>
    /// <remarks>The default is <see langword="null"/>. Item templates determine its presentation.</remarks>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(BreadcrumbPathSegment),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="PageType"/> dependency property.</summary>
    /// <remarks>The default is <see langword="null"/> and it does not affect layout or rendering.</remarks>
    public static readonly DependencyProperty PageTypeProperty = DependencyProperty.Register(
        nameof(PageType),
        typeof(Type),
        typeof(BreadcrumbPathSegment),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="RouteKey"/> dependency property.</summary>
    /// <remarks>The default is <see langword="null"/> and it does not affect layout or rendering.</remarks>
    public static readonly DependencyProperty RouteKeyProperty = DependencyProperty.Register(
        nameof(RouteKey),
        typeof(string),
        typeof(BreadcrumbPathSegment),
        new PropertyMetadata(null),
        value => value is null || !string.IsNullOrWhiteSpace((string)value)
    );

    /// <summary>Identifies the <see cref="Parameter"/> dependency property.</summary>
    /// <remarks>The default is <see langword="null"/> and it does not affect layout or rendering.</remarks>
    public static readonly DependencyProperty ParameterProperty = DependencyProperty.Register(
        nameof(Parameter),
        typeof(object),
        typeof(BreadcrumbPathSegment),
        new PropertyMetadata(null)
    );

    /// <summary>Initializes a new instance of the <see cref="BreadcrumbPathSegment"/> class.</summary>
    public BreadcrumbPathSegment() { }

    /// <summary>Gets or sets the stable identity of this segment within the declared path.</summary>
    /// <remarks>A non-empty value is required before the active path is resolved.</remarks>
    public string? Key
    {
        get => (string?)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>Gets or sets the value presented by the breadcrumb item template.</summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Gets or sets the optional page type activated by this segment.</summary>
    public Type? PageType
    {
        get => (Type?)GetValue(PageTypeProperty);
        set => SetValue(PageTypeProperty, value);
    }

    /// <summary>Gets or sets the optional map route activated by this segment.</summary>
    public string? RouteKey
    {
        get => (string?)GetValue(RouteKeyProperty);
        set => SetValue(RouteKeyProperty, value);
    }

    /// <summary>Gets or sets optional occurrence data supplied when this segment is invoked.</summary>
    public object? Parameter
    {
        get => GetValue(ParameterProperty);
        set => SetValue(ParameterProperty, value);
    }

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore() => new BreadcrumbPathSegment();

    internal BreadcrumbSegment CreateSegment()
    {
        string key = Key
            ?? throw new InvalidOperationException(
                "Every declared breadcrumb path segment must have a non-empty Key."
            );
        NavigationTarget? target = PageType is not null || RouteKey is not null
            ? new NavigationTarget(PageType, RouteKey, Parameter)
            : null;
        return new BreadcrumbSegment(key, Content, target);
    }
}
