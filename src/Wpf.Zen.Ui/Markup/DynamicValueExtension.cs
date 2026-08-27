// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a WPF dynamic resource reference for a strongly typed <see cref="ValueToken"/>.
/// </summary>
/// <remarks>
/// Value tokens cover theme metrics such as font sizes, corner radii, thicknesses, and padding.
/// The enum constructor preserves XAML IntelliSense while the dynamic reference follows dictionary replacement.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(object))]
public sealed class DynamicValueExtension : DynamicResourceExtension
{
    /// <summary>Gets or sets the logical theme value token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public ValueToken Token
    {
        get;
        set
        {
            var resourceKey = ValueTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    public DynamicValueExtension()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DynamicValueExtension"/> class for the specified token.</summary>
    /// <param name="token">The theme value role to resolve from the active resource scope.</param>
    public DynamicValueExtension(ValueToken token)
    {
        Token = token;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal static class ValueTokenResourceKey
{
    internal static ComponentResourceKey Get(ValueToken token)
    {
        return token switch
        {
            ValueToken.DefaultIconFontSize                      => ValueKeys.DefaultIconFontSize,
            ValueToken.ControlContentThemeFontSize              => ValueKeys.ControlContentThemeFontSize,
            ValueToken.ControlCornerRadius                      => ValueKeys.ControlCornerRadius,
            ValueToken.OverlayCornerRadius                      => ValueKeys.OverlayCornerRadius,
            ValueToken.PopupCornerRadius                        => ValueKeys.PopupCornerRadius,
            ValueToken.TextControlBorderThemeThickness          => ValueKeys.TextControlBorderThemeThickness,
            ValueToken.TextControlBorderThemeThicknessFocused   => ValueKeys.TextControlBorderThemeThicknessFocused,
            ValueToken.TextControlThemePadding                  => ValueKeys.TextControlThemePadding,
            ValueToken.ButtonThemePadding                       => ValueKeys.ButtonThemePadding,
            ValueToken.ContentControlFontSize                   => ValueKeys.ContentControlFontSize,
            ValueToken.ListViewItemMinHeight                    => ValueKeys.ListViewItemMinHeight,
            ValueToken.TreeViewItemMinHeight                    => ValueKeys.TreeViewItemMinHeight,
            ValueToken.TreeViewItemMultiSelectCheckBoxMinHeight => ValueKeys.TreeViewItemMultiSelectCheckBoxMinHeight,
            ValueToken.TreeViewItemPresenterMargin              => ValueKeys.TreeViewItemPresenterMargin,
            ValueToken.TreeViewItemPresenterPadding             => ValueKeys.TreeViewItemPresenterPadding,
            ValueToken.TimePickerHostPadding                    => ValueKeys.TimePickerHostPadding,
            ValueToken.DatePickerHostPadding                    => ValueKeys.DatePickerHostPadding,
            ValueToken.DatePickerHostMonthPadding               => ValueKeys.DatePickerHostMonthPadding,
            ValueToken.ComboBoxMinHeight                        => ValueKeys.ComboBoxMinHeight,
            ValueToken.NavigationViewItemOnLeftMinHeight        => ValueKeys.NavigationViewItemOnLeftMinHeight,

            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "The value token is not registered."),
        };
    }
}
#pragma warning restore SA1402 // File may only contain a single type
