// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(Style))]
public class StyleResourceExtension : DynamicResourceExtension
{
    [ConstructorArgument("id")]
    public StyleId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = KeyMapping(value);
        }
    }

    public StyleResourceExtension(StyleId id)
    {
        Id = id;
    }

    internal static ComponentResourceKey KeyMapping(StyleId id)
    {
        return id switch
        {
            StyleId.MenuStyle                       => StyleKeys.MenuStyle,
            StyleId.UiMenuStyle                     => StyleKeys.UiMenuStyle,
            StyleId.MenuItemStyle                   => StyleKeys.MenuItemStyle,
            StyleId.UiMenuItemStyle                 => StyleKeys.UiMenuItemStyle,
            StyleId.MenuItemSeparatorStyle          => StyleKeys.MenuItemSeparatorStyle,
            StyleId.DefaultCheckBoxStyle            => StyleKeys.DefaultCheckBoxStyle,
            StyleId.DefaultRadioButtonStyle         => StyleKeys.DefaultRadioButtonStyle,
            StyleId.DefaultComboBoxStyle            => StyleKeys.DefaultComboBoxStyle,
            StyleId.DefaultComboBoxItemStyle        => StyleKeys.DefaultComboBoxItemStyle,
            StyleId.UiComboBoxStyle                 => StyleKeys.UiComboBoxStyle,
            StyleId.DefaultTextBoxStyle             => StyleKeys.DefaultTextBoxStyle,
            StyleId.DefaultUiTextBoxStyle           => StyleKeys.DefaultUiTextBoxStyle,
            StyleId.DefaultRichTextBoxStyle         => StyleKeys.DefaultRichTextBoxStyle,
            StyleId.UiRichTextBoxStyle              => StyleKeys.UiRichTextBoxStyle,
            StyleId.DefaultTextBoxScrollViewerStyle => StyleKeys.DefaultTextBoxScrollViewerStyle,
            StyleId.DefaultControlFocusVisualStyle  => StyleKeys.DefaultControlFocusVisualStyle,
            StyleId.DefaultScrollBarStyle           => StyleKeys.DefaultScrollBarStyle,
            StyleId.DynamicScrollPageStyle          => StyleKeys.DynamicScrollPageStyle,
            StyleId.NavigationViewItemDataTemplate  => StyleKeys.NavigationViewItemDataTemplate,
            StyleId.IconInfoBadgeStyle              => StyleKeys.IconInfoBadgeStyle,
            StyleId.ValueInfoBadgeStyle             => StyleKeys.ValueInfoBadgeStyle,
            StyleId.DotInfoBadgeStyle               => StyleKeys.DotInfoBadgeStyle,
            StyleId.DefaultCalendarStyle            => StyleKeys.DefaultCalendarStyle,
            StyleId.DefaultDatePickerStyle          => StyleKeys.DefaultDatePickerStyle,
            StyleId.UiSplitButtonStyle              => StyleKeys.UiSplitButtonStyle,
            StyleId.DefaultProgressBarStyle         => StyleKeys.DefaultProgressBarStyle,

            StyleId.DefaultValidationTemplate       => StyleKeys.DefaultValidationTemplate,

            StyleId.Unknown => throw new NotSupportedException(),
            _               => throw new IndexOutOfRangeException(),
        };
    }
}