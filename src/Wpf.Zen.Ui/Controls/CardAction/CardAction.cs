// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Inherited from the <see cref="ButtonBase"/> interactive card styled according to Fluent Design.
/// </summary>
public class CardAction : ButtonBase
{
    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(CardAction),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>Identifies the <see cref="IconMargin"/> dependency property.</summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(CardAction),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

    /// <summary>Identifies the <see cref="IsChevronVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsChevronVisibleProperty = DependencyProperty.Register(
        nameof(IsChevronVisible),
        typeof(bool),
        typeof(CardAction),
        new PropertyMetadata(true)
    );

    static CardAction()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CardAction),
            new FrameworkPropertyMetadata(typeof(CardAction))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether to display the chevron icon on the right side of the card.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public bool IsChevronVisible
    {
        get => (bool)GetValue(IsChevronVisibleProperty);
        set => SetValue(IsChevronVisibleProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new CardActionAutomationPeer(this);
    }
}
