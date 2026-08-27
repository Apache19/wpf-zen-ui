// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a WPF dynamic resource reference for a strongly typed <see cref="StyleToken"/>.
/// </summary>
/// <remarks>
/// Because the constructor argument is an enum, XAML editors can suggest every published style token.
/// Dynamic lookup should be used when replacement of the resource dictionary must update an existing control.
/// Use <see cref="StaticStyleExtension"/> when the target is a CLR property such as <see cref="Style.BasedOn"/>.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Style))]
public sealed class DynamicStyleExtension : DynamicResourceExtension
{
    /// <summary>Gets or sets the logical style token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public StyleToken Token
    {
        get;
        set
        {
            var resourceKey = StyleTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    public DynamicStyleExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicStyleExtension"/> class for the specified token.
    /// </summary>
    /// <param name="token">The style role to resolve from the active resource scope.</param>
    public DynamicStyleExtension(StyleToken token)
    {
        Token = token;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal static class StyleTokenResourceKey
{
    internal static ComponentResourceKey Get(StyleToken token)
    {
        return token switch
        {
            StyleToken.Menu                       => StyleKeys.MenuStyle,
            StyleToken.UiMenu                     => StyleKeys.UiMenuStyle,
            StyleToken.MenuItem                   => StyleKeys.MenuItemStyle,
            StyleToken.UiMenuItem                 => StyleKeys.UiMenuItemStyle,
            StyleToken.MenuItemSeparator          => StyleKeys.MenuItemSeparatorStyle,
            StyleToken.DefaultCheckBox            => StyleKeys.DefaultCheckBoxStyle,
            StyleToken.DefaultRadioButton         => StyleKeys.DefaultRadioButtonStyle,
            StyleToken.DefaultComboBox            => StyleKeys.DefaultComboBoxStyle,
            StyleToken.DefaultComboBoxItem        => StyleKeys.DefaultComboBoxItemStyle,
            StyleToken.UiComboBox                 => StyleKeys.UiComboBoxStyle,
            StyleToken.DefaultTextBox             => StyleKeys.DefaultTextBoxStyle,
            StyleToken.DefaultUiTextBox           => StyleKeys.DefaultUiTextBoxStyle,
            StyleToken.DefaultRichTextBox         => StyleKeys.DefaultRichTextBoxStyle,
            StyleToken.UiRichTextBox              => StyleKeys.UiRichTextBoxStyle,
            StyleToken.DefaultControlFocusVisual  => StyleKeys.DefaultControlFocusVisualStyle,
            StyleToken.DefaultScrollViewer        => StyleKeys.DefaultScrollViewerStyle,
            StyleToken.UiDynamicScrollViewer      => StyleKeys.UiDynamicScrollViewerStyle,
            StyleToken.IconInfoBadge              => StyleKeys.IconInfoBadgeStyle,
            StyleToken.ValueInfoBadge             => StyleKeys.ValueInfoBadgeStyle,
            StyleToken.DotInfoBadge               => StyleKeys.DotInfoBadgeStyle,
            StyleToken.DefaultCalendar            => StyleKeys.DefaultCalendarStyle,
            StyleToken.DefaultDatePicker          => StyleKeys.DefaultDatePickerStyle,
            StyleToken.UiSplitButton              => StyleKeys.UiSplitButtonStyle,
            StyleToken.UiAutoSuggestBox           => StyleKeys.UiAutoSuggestBoxStyle,
            StyleToken.UiBadge                    => StyleKeys.UiBadgeStyle,
            StyleToken.DefaultProgressBar         => StyleKeys.DefaultProgressBarStyle,
            StyleToken.GridViewListView           => StyleKeys.GridViewListViewStyle,
            StyleToken.GridViewItemContainer      => StyleKeys.GridViewItemContainerStyle,
            StyleToken.GridViewColumnHeader       => StyleKeys.GridViewColumnHeaderStyle,
            StyleToken.GridViewScrollViewer       => StyleKeys.GridViewScrollViewerStyle,

            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "The style token is not registered."),
        };
    }
}
#pragma warning restore SA1402 // File may only contain a single type
