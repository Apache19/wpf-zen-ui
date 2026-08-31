// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Allows to rate positively or negatively by clicking on one of the thumbs.
/// </summary>
[TemplatePart(Name = LikeButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = DislikeButtonPartName, Type = typeof(ButtonBase))]
public class ThumbRate : System.Windows.Controls.Control
{
    private const string LikeButtonPartName = "PART_LikeButton";
    private const string DislikeButtonPartName = "PART_DislikeButton";

    private ButtonBase? _likeButton;
    private ButtonBase? _dislikeButton;

    /// <summary>Identifies the <see cref="State"/> dependency property.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ThumbRateState),
        typeof(ThumbRate),
        new PropertyMetadata(ThumbRateState.None, OnStateChanged)
    );

    /// <summary>Identifies the <see cref="StateChanged"/> routed event.</summary>
    public static readonly RoutedEvent StateChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(StateChanged),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<ThumbRate, RoutedEventArgs>),
        typeof(ThumbRate)
    );

    static ThumbRate()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ThumbRate),
            new FrameworkPropertyMetadata(typeof(ThumbRate))
        );
    }

    /// <summary>
    /// Occurs when <see cref="State"/> is changed.
    /// </summary>
    public event TypedEventHandler<ThumbRate, RoutedEventArgs> StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <summary>
    /// Gets or sets the value determining the current state of the control.
    /// </summary>
    public ThumbRateState State
    {
        get => (ThumbRateState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Updates the rating after the user activates one of the template buttons.
    /// </summary>
    /// <param name="state">The state represented by the activated button.</param>
    protected virtual void OnButtonClick(ThumbRateState state)
    {
        if (State == state)
        {
            SetCurrentValue(StateProperty, ThumbRateState.None);
            return;
        }

        SetCurrentValue(StateProperty, state);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Both buttons are optional template parts. Resolving them on each application keeps event
    /// ownership aligned with WPF's replaceable template visual tree.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachTemplateButtons();
        base.OnApplyTemplate();

        _likeButton = GetTemplateChild(LikeButtonPartName) as ButtonBase;
        _dislikeButton = GetTemplateChild(DislikeButtonPartName) as ButtonBase;

        if (_likeButton is not null)
        {
            _likeButton.Click += OnLikeButtonClick;
        }

        if (_dislikeButton is not null)
        {
            _dislikeButton.Click += OnDislikeButtonClick;
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        DetachTemplateButtons();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// This virtual method is called when <see cref="State"/> is changed.
    /// </summary>
    protected virtual void OnStateChanged(ThumbRateState previousState, ThumbRateState currentState)
    {
        RaiseEvent(new RoutedEventArgs(StateChangedEvent, this));
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ThumbRate thumbRate)
        {
            return;
        }

        thumbRate.OnStateChanged((ThumbRateState)e.OldValue, (ThumbRateState)e.NewValue);
    }

    private void OnLikeButtonClick(object sender, RoutedEventArgs e)
    {
        OnButtonClick(ThumbRateState.Liked);
    }

    private void OnDislikeButtonClick(object sender, RoutedEventArgs e)
    {
        OnButtonClick(ThumbRateState.Disliked);
    }

    private void DetachTemplateButtons()
    {
        if (_likeButton is not null)
        {
            _likeButton.Click -= OnLikeButtonClick;
            _likeButton = null;
        }

        if (_dislikeButton is not null)
        {
            _dislikeButton.Click -= OnDislikeButtonClick;
            _dislikeButton = null;
        }
    }
}
