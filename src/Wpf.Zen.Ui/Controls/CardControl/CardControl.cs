// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using Wpf.Zen.Ui.AutomationPeers;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Inherited from the <see cref="System.Windows.Controls.Primitives.ButtonBase"/> control which displays an additional control on the right side of the card.
/// </summary>
public class CardControl : System.Windows.Controls.Primitives.ButtonBase
{
    /// <summary>Identifies the <see cref="Header"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(CardControl),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(CardControl),
        new PropertyMetadata(new CornerRadius(0))
    );

    static CardControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CardControl),
            new FrameworkPropertyMetadata(typeof(CardControl))
        );
    }

    /// <summary>
    /// Gets or sets header which is used for each item in the control.
    /// </summary>
    [Bindable(true)]
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius of the control.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new CardControlAutomationPeer(this);
    }
}
