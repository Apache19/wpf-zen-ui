// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Defines dialog content, action and lifecycle properties. Template bindings apply visual and layout changes; properties are not inherited.</summary>
[StyleTypedProperty(Property = nameof(CloseButtonStyle), StyleTargetType = typeof(Button))]
public partial class ContentDialog
{
    #region DP: Title

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the heading content. The default is null; the template measures the heading.</summary>
    public object? Title
    {
        get => (object?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region DP: TitleTemplate

    /// <summary>Identifies the <see cref="TitleTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty TitleTemplateProperty = DependencyProperty.Register(
        nameof(TitleTemplate),
        typeof(DataTemplate),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the heading template, which takes precedence over TitleTemplateSelector. Default null lets WPF select the presentation.</summary>
    public DataTemplate? TitleTemplate
    {
        get => (DataTemplate?)GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    #endregion

    #region DP: TitleTemplateSelector

    /// <summary>Identifies the <see cref="TitleTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty TitleTemplateSelectorProperty = DependencyProperty.Register(
        nameof(TitleTemplateSelector),
        typeof(DataTemplateSelector),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the selector used when TitleTemplate is null. Default null uses WPF's implicit template lookup.</summary>
    /// <remarks>
    /// The title ContentPresenter performs selection and remeasures when its presentation changes.
    /// This property is not inherited and has no coercion or change callback. Discrete animation
    /// values are forwarded to the presenter in the same way as ordinary property changes.
    /// Custom templates can use ContentSource="Title" to forward all title presentation properties.
    /// </remarks>
    public DataTemplateSelector? TitleTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(TitleTemplateSelectorProperty);
        set => SetValue(TitleTemplateSelectorProperty, value);
    }

    #endregion

    #region DP: TitleStringFormat

    /// <summary>Identifies the <see cref="TitleStringFormat"/> dependency property.</summary>
    public static readonly DependencyProperty TitleStringFormatProperty = DependencyProperty.Register(
        nameof(TitleStringFormat),
        typeof(string),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the composite format used by WPF's default text presentation of Title. Default null applies no explicit format.</summary>
    /// <remarks>
    /// A selected or explicit DataTemplate owns its own formatting; UIElement titles are not formatted.
    /// The title ContentPresenter handles formatting, culture and resulting layout changes.
    /// This property is not inherited and has no coercion or change callback. Discrete string
    /// animation values are forwarded to the presenter in the same way as ordinary changes.
    /// </remarks>
    public string? TitleStringFormat
    {
        get => (string?)GetValue(TitleStringFormatProperty);
        set => SetValue(TitleStringFormatProperty, value);
    }

    #endregion

    #region DP: PrimaryAction

    /// <summary>Identifies the <see cref="PrimaryAction"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryActionProperty = DependencyProperty.Register(
        nameof(PrimaryAction),
        typeof(ContentDialogAction),
        typeof(ContentDialog),
        new PropertyMetadata(null, OnActionChanged)
    );

    /// <summary>Gets or sets the primary footer action. Null (the default) removes its button from layout. Changes invalidate command availability.</summary>
    public ContentDialogAction? PrimaryAction
    {
        get => (ContentDialogAction?)GetValue(PrimaryActionProperty);
        set => SetValue(PrimaryActionProperty, value);
    }

    #endregion

    #region DP: SecondaryAction

    /// <summary>Identifies the <see cref="SecondaryAction"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryActionProperty = DependencyProperty.Register(
        nameof(SecondaryAction),
        typeof(ContentDialogAction),
        typeof(ContentDialog),
        new PropertyMetadata(null, OnActionChanged)
    );

    /// <summary>Gets or sets the secondary footer action. Null (the default) removes its button from layout. Changes invalidate command availability.</summary>
    public ContentDialogAction? SecondaryAction
    {
        get => (ContentDialogAction?)GetValue(SecondaryActionProperty);
        set => SetValue(SecondaryActionProperty, value);
    }

    #endregion

    #region DP: TertiaryAction

    /// <summary>Identifies the <see cref="TertiaryAction"/> dependency property.</summary>
    public static readonly DependencyProperty TertiaryActionProperty = DependencyProperty.Register(
        nameof(TertiaryAction),
        typeof(ContentDialogAction),
        typeof(ContentDialog),
        new PropertyMetadata(null, OnActionChanged)
    );

    /// <summary>Gets or sets the tertiary footer action. Null (the default) removes its button from layout. Changes invalidate command availability.</summary>
    public ContentDialogAction? TertiaryAction
    {
        get => (ContentDialogAction?)GetValue(TertiaryActionProperty);
        set => SetValue(TertiaryActionProperty, value);
    }

    #endregion

    #region DP: DefaultButton

    /// <summary>Identifies the <see cref="DefaultButton"/> dependency property.</summary>
    public static readonly DependencyProperty DefaultButtonProperty = DependencyProperty.Register(
        nameof(DefaultButton),
        typeof(ContentDialogButton),
        typeof(ContentDialog),
        new PropertyMetadata(ContentDialogButton.None, OnCommandStateChanged),
        value => value is ContentDialogButton.None or ContentDialogButton.Primary or ContentDialogButton.Secondary or ContentDialogButton.Tertiary
    );

    /// <summary>Gets or sets the Enter action. The default is None; disabled or absent actions never fall back to a different choice.</summary>
    public ContentDialogButton DefaultButton
    {
        get => (ContentDialogButton)GetValue(DefaultButtonProperty);
        set => SetValue(DefaultButtonProperty, value);
    }

    #endregion

    #region DP: IsDismissible

    /// <summary>Identifies the <see cref="IsDismissible"/> dependency property.</summary>
    public static readonly DependencyProperty IsDismissibleProperty = DependencyProperty.Register(
        nameof(IsDismissible),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(true, OnCommandStateChanged)
    );

    /// <summary>Gets or sets a value indicating whether the header close button, Escape and automation may dismiss the dialog. Default true. This does not prevent Hide, cancellation or removal from the tree.</summary>
    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    #endregion

    #region DP: CloseButtonStyle

    /// <summary>Identifies the <see cref="CloseButtonStyle"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonStyleProperty = DependencyProperty.Register(
        nameof(CloseButtonStyle),
        typeof(Style),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the style applied to PART_CloseButton, including localized tooltip and automation name. Default null uses the part's template-supplied style.</summary>
    /// <remarks>The control binds this property when the template is applied. Custom templates need only retain the optional button part's name; subsequent style changes update it automatically.</remarks>
    public Style? CloseButtonStyle
    {
        get => (Style?)GetValue(CloseButtonStyleProperty);
        set => SetValue(CloseButtonStyleProperty, value);
    }

    #endregion

    #region EVENT: Opened

    /// <summary>Identifies the <see cref="Opened"/> routed event.</summary>
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(ContentDialog)
    );

    /// <summary>Occurs once after the dialog is loaded and its entrance transition completes. A dialog dismissed during entrance does not raise this event.</summary>
    public event RoutedEventHandler Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    #endregion

    #region EVENT: Closed

    /// <summary>Identifies the <see cref="Closed"/> routed event.</summary>
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed),
        RoutingStrategy.Direct,
        typeof(EventHandler<ContentDialogClosedEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>Occurs once after presentation cleanup. This direct notification cannot cancel closure. Without an accepted choice, removal and cancellation report None.</summary>
    /// <remarks>Subscribe on the dialog itself; its parent route no longer exists after removal.</remarks>
    public event EventHandler<ContentDialogClosedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    #region EVENT: ButtonClicked

    /// <summary>Identifies the <see cref="ButtonClicked"/> routed event.</summary>
    public static readonly RoutedEvent ButtonClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(ButtonClicked),
        RoutingStrategy.Bubble,
        typeof(EventHandler<ContentDialogButtonClickEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>Occurs before a footer action executes. Cancel rejects only this action; it cannot veto Hide or visual-tree removal.</summary>
    public event EventHandler<ContentDialogButtonClickEventArgs> ButtonClicked
    {
        add => AddHandler(ButtonClickedEvent, value);
        remove => RemoveHandler(ButtonClickedEvent, value);
    }

    #endregion

    #region DP: IsOpen

    private static readonly DependencyPropertyKey IsOpenPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsOpen),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the read-only <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = IsOpenPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether a presentation is reserved, including entrance, exit and Closed notification. Default false; this state does not affect layout.</summary>
    public bool IsOpen => (bool)GetValue(IsOpenProperty);

    #endregion

    #region DP: IsClosing

    private static readonly DependencyPropertyKey IsClosingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsClosing),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the read-only <see cref="IsClosing"/> dependency property.</summary>
    public static readonly DependencyProperty IsClosingProperty = IsClosingPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether a close result has been accepted and presentation cleanup is pending. Default false; this state does not affect layout.</summary>
    public bool IsClosing => (bool)GetValue(IsClosingProperty);

    #endregion

}
