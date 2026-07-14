// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a renderable icon element whose visual content is created by an <see cref="IconSource"/>.
/// </summary>
/// <remarks>
/// The element keeps a stable presenter even when <see cref="IconSource"/> is <see langword="null"/>.
/// This is required by <see cref="IconElement"/>, whose visual-tree contract always exposes one
/// child. Assigning, replacing, or clearing the source updates only the presenter's content and
/// therefore remains safe after WPF has already measured or attached the element.
/// </remarks>
[ContentProperty(nameof(IconSource))]
public class IconSourceElement : IconElement
{
    /// <summary>
    /// Identifies the <see cref="IconSource"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="null"/>. The internal presenter observes changes through
    /// a binding, so replacing the source also invalidates measurement through
    /// <see cref="ContentPresenter.Content"/>.
    /// </remarks>
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource),
        typeof(IconSource),
        typeof(IconSourceElement),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the source used to create the displayed icon.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value is valid and produces an empty element. The source may be
    /// assigned or cleared before or after the element enters the visual tree.
    /// </remarks>
    public IconSource? IconSource
    {
        get => (IconSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    /// <summary>
    /// Creates the one stable child required by <see cref="IconElement"/>. The presenter, rather
    /// than the source-created icon, is the structural child because the generated icon is optional
    /// and can change during the element's lifetime.
    /// </summary>
    protected override UIElement InitializeChildren()
    {
        var iconPresenter = new ContentPresenter
        {
            Focusable = false,
            SnapsToDevicePixels = SnapsToDevicePixels,
        };

        // A binding survives ContentPresenter's first template initialization, unlike a current-
        // value modifier applied before the presenter enters the visual tree. It also centralizes
        // every nullable transition without requiring a second structural child or manual refresh.
        BindingOperations.SetBinding(
            iconPresenter,
            ContentPresenter.ContentProperty,
            new Binding(nameof(IconSource))
            {
                Source = this,
                Converter = IconSourceToIconElementConverter.Instance,
            }
        );

        return iconPresenter;
    }

    /// <summary>
    /// Creates a new icon element from the current <see cref="IconSource"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="IconElement"/> instance, or <see langword="null"/> when no source is set.
    /// </returns>
    /// <remarks>
    /// Each call asks the source for a new element so callers can safely place the result under a
    /// different visual parent. Rendering this <see cref="IconSourceElement"/> directly uses the
    /// same method and hosts the result in its private presenter.
    /// </remarks>
    public IconElement? CreateIconElement()
    {
        return IconSource?.CreateIconElement();
    }

    /// <summary>
    /// Converts source state into a fresh visual element for the private presenter. Keeping this
    /// conversion private prevents callers from depending on an implementation-only binding helper.
    /// </summary>
    private sealed class IconSourceToIconElementConverter : IValueConverter
    {
        public static IconSourceToIconElementConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is IconSource iconSource ? iconSource.CreateIconElement() : null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException(
                $"{nameof(IconSourceElement)} only converts IconSource values for presentation."
            );
        }
    }
}
