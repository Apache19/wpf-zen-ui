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
