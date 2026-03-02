// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(object))]
public class VariableResourceExtension : DynamicResourceExtension
{
    [ConstructorArgument("id")]
    public VariableId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = KeyMapping(value);
        }
    }

    public VariableResourceExtension(VariableId id)
    {
        Id = id;
    }

    private static ComponentResourceKey KeyMapping(VariableId id)
    {
        return id switch
        {
            VariableId.DefaultIconFontSize                      => VariableKeys.DefaultIconFontSize,
            VariableId.ControlContentThemeFontSize              => VariableKeys.ControlContentThemeFontSize,
            VariableId.ControlCornerRadius                      => VariableKeys.ControlCornerRadius,
            VariableId.OverlayCornerRadius                      => VariableKeys.OverlayCornerRadius,
            VariableId.PopupCornerRadius                        => VariableKeys.PopupCornerRadius,
            VariableId.TextControlBorderThemeThickness          => VariableKeys.TextControlBorderThemeThickness,
            VariableId.TextControlBorderThemeThicknessFocused   => VariableKeys.TextControlBorderThemeThicknessFocused,
            VariableId.TextControlThemePadding                  => VariableKeys.TextControlThemePadding,
            VariableId.ContentControlFontSize                   => VariableKeys.ContentControlFontSize,
            VariableId.TextControlThemeMinHeight                => VariableKeys.TextControlThemeMinHeight,
            VariableId.TextControlThemeMinWidth                 => VariableKeys.TextControlThemeMinWidth,
            VariableId.ListViewItemMinHeight                    => VariableKeys.ListViewItemMinHeight,
            VariableId.TreeViewItemMinHeight                    => VariableKeys.TreeViewItemMinHeight,
            VariableId.TreeViewItemMultiSelectCheckBoxMinHeight => VariableKeys.TreeViewItemMultiSelectCheckBoxMinHeight,
            VariableId.TreeViewItemPresenterMargin              => VariableKeys.TreeViewItemPresenterMargin,
            VariableId.TreeViewItemPresenterPadding             => VariableKeys.TreeViewItemPresenterPadding,
            VariableId.TimePickerHostPadding                    => VariableKeys.TimePickerHostPadding,
            VariableId.DatePickerHostPadding                    => VariableKeys.DatePickerHostPadding,
            VariableId.DatePickerHostMonthPadding               => VariableKeys.DatePickerHostMonthPadding,
            VariableId.ComboBoxMinHeight                        => VariableKeys.ComboBoxMinHeight,
            VariableId.NavigationViewItemOnLeftMinHeight        => VariableKeys.NavigationViewItemOnLeftMinHeight,

            VariableId.Unknown => throw new NotSupportedException(),
            _                  => throw new IndexOutOfRangeException(),
        };
    }
}
