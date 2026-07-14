// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the base class for an icon UI element.
/// </summary>
[TypeConverter(typeof(IconElementConverter))]
public abstract class IconElement : FrameworkElement
{
    static IconElement()
    {
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(IconElement),
            new FrameworkPropertyMetadata(false)
        );

        SnapsToDevicePixelsProperty.OverrideMetadata(
            typeof(IconElement),
            new FrameworkPropertyMetadata(true)
        );
    }

    /// <summary>Identifies the <see cref="Foreground"/> dependency property.</summary>
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(IconElement),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits,
            static (d, args) => ((IconElement)d).OnForegroundChanged(args)
        )
    );

    /// <inheritdoc cref="TextElement.Foreground"/>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override int VisualChildrenCount => 1;

    private UIElement? _child;

    protected abstract UIElement InitializeChildren();

    protected virtual void OnForegroundChanged(DependencyPropertyChangedEventArgs args) { }

    /// <summary>
    /// Creates and attaches the icon's single visual child on first use.
    /// </summary>
    /// <remarks>
    /// <see cref="FrameworkElement"/> already provides the layout boundary that an icon needs.
    /// Hosting the child directly avoids allocating an otherwise redundant panel for every icon.
    /// The child remains stable after creation because WPF may enumerate the visual tree at any
    /// point after measurement begins.
    /// </remarks>
    private void EnsureChild()
    {
        if (_child != null)
        {
            return;
        }

        _child = InitializeChildren();
        AddVisualChild(_child);
    }

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "IconElement should have only 1 child");
        }

        EnsureChild();
        return _child!;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureChild();

        _child!.Measure(availableSize);
        return _child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureChild();

        _child!.Arrange(new Rect(default, finalSize));
        return finalSize;
    }

    /// <summary>
    /// Coerces the value of an Icon dependency property, allowing the use of either IconElement or IconSourceElement.
    /// </summary>
    /// <param name="_">The dependency object (unused).</param>
    /// <param name="baseValue">The value to be coerced.</param>
    /// <returns>An IconElement, either directly or derived from an IconSourceElement.</returns>
    public static object? Coerce(DependencyObject _, object? baseValue)
    {
        return baseValue switch
        {
            IconSourceElement iconSourceElement => iconSourceElement.CreateIconElement(),
            IconElement or null => baseValue,
            _ => throw new ArgumentException(
                message: $"Expected either '{typeof(IconSourceElement)}' or '{typeof(IconElement)}' but got '{baseValue.GetType()}'.",
                paramName: nameof(baseValue)
            ),
        };
    }
}
