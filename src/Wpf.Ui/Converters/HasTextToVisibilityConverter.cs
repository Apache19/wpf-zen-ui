// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Ui.Converters;

/// <summary>
/// Converts a string value to a <see cref="Visibility"/> based on whether the string
/// contains text.
/// </summary>
/// <remarks>
/// A string is considered to have no text when it is <c>null</c> or contains only
/// whitespace characters (unless <see cref="AllowWhiteSpace"/> is enabled).
///
/// <para>
/// Default behavior:
/// <list type="bullet">
/// <item>
/// <description>
/// Strings that contain text return <see cref="Visibility.Visible"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// Empty strings return <see cref="Visibility.Collapsed"/>.
/// </description>
/// </item>
/// </list>
/// </para>
///
/// <para>
/// The behavior can be customized:
/// <list type="bullet">
/// <item>
/// <description>
/// Set <see cref="UseHidden"/> to return <see cref="Visibility.Hidden"/> instead of
/// <see cref="Visibility.Collapsed"/> for empty values.
/// </description>
/// </item>
/// <item>
/// <description>
/// Set <see cref="Invert"/> to invert the result (swap
/// <see cref="Visibility.Visible"/> with the empty-state visibility).
/// </description>
/// </item>
/// <item>
/// <description>
/// Set <see cref="AllowWhiteSpace"/> to treat whitespace-only strings as text.
/// </description>
/// </item>
/// </list>
/// </para>
///
/// This converter is typically used to control element visibility based on
/// whether an input field contains text.
/// </remarks>
[MarkupExtensionReturnType(typeof(HasTextToVisibilityConverter))]
public class HasTextToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether empty values return
    /// <see cref="Visibility.Hidden"/> instead of <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <value>
    /// <c>true</c> to return <see cref="Visibility.Hidden"/>; otherwise
    /// <see cref="Visibility.Collapsed"/> (default).
    /// </value>
    public bool UseHidden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversion result is inverted.
    /// </summary>
    /// <remarks>
    /// When enabled, <see cref="Visibility.Visible"/> and the empty-state
    /// visibility (<see cref="Visibility.Hidden"/> or
    /// <see cref="Visibility.Collapsed"/>) are swapped.
    /// </remarks>
    public bool Invert { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether whitespace-only strings are treated
    /// as containing text.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>false</c> (default): Uses <see cref="string.IsNullOrWhiteSpace(string?)"/>,
    /// so whitespace-only strings are treated as empty.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>true</c>: Uses <see cref="string.IsNullOrEmpty(string?)"/>,
    /// so whitespace-only strings are treated as containing text.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public bool AllowWhiteSpace { get; set; } = true;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;

        // Choose the emptiness check based on AllowWhiteSpace
        var isEmpty = AllowWhiteSpace
            ? string.IsNullOrEmpty(s)
            : string.IsNullOrWhiteSpace(s);

        // Apply inversion to the condition
        if (Invert)
        {
            isEmpty = !isEmpty;
        }

        return isEmpty
            ? UseHidden ? Visibility.Hidden : Visibility.Collapsed
            : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("HasTextToVisibilityConverter does not support ConvertBack.");
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}