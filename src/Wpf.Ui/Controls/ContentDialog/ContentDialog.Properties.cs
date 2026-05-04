// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Partial class <c>ContentDialog.Properties</c>
/// A partial class of <see cref="ContentDialog"/> specifically for defining its dependency properties.
/// </summary>
public partial class ContentDialog
{
    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="TitleTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty TitleTemplateProperty = DependencyProperty.Register(
        nameof(TitleTemplate),
        typeof(DataTemplate),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="DialogWidth"/> dependency property.</summary>
    public static readonly DependencyProperty DialogWidthProperty = DependencyProperty.Register(
        nameof(DialogWidth),
        typeof(double),
        typeof(ContentDialog),
        new PropertyMetadata(double.PositiveInfinity)
    );

    /// <summary>Identifies the <see cref="DialogHeight"/> dependency property.</summary>
    public static readonly DependencyProperty DialogHeightProperty = DependencyProperty.Register(
        nameof(DialogHeight),
        typeof(double),
        typeof(ContentDialog),
        new PropertyMetadata(double.PositiveInfinity)
    );

    /// <summary>Identifies the <see cref="DialogMaxWidth"/> dependency property.</summary>
    public static readonly DependencyProperty DialogMaxWidthProperty = DependencyProperty.Register(
        nameof(DialogMaxWidth),
        typeof(double),
        typeof(ContentDialog),
        new PropertyMetadata(double.PositiveInfinity)
    );

    /// <summary>Identifies the <see cref="DialogMaxHeight"/> dependency property.</summary>
    public static readonly DependencyProperty DialogMaxHeightProperty = DependencyProperty.Register(
        nameof(DialogMaxHeight),
        typeof(double),
        typeof(ContentDialog),
        new PropertyMetadata(double.PositiveInfinity)
    );

    /// <summary>Identifies the <see cref="DialogMargin"/> dependency property.</summary>
    public static readonly DependencyProperty DialogMarginProperty = DependencyProperty.Register(
        nameof(DialogMargin),
        typeof(Thickness),
        typeof(ContentDialog)
    );

    /// <summary>Identifies the <see cref="PrimaryButtonText"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register(
        nameof(PrimaryButtonText),
        typeof(string),
        typeof(ContentDialog),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>Identifies the <see cref="SecondaryButtonText"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryButtonTextProperty = DependencyProperty.Register(
        nameof(SecondaryButtonText),
        typeof(string),
        typeof(ContentDialog),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>Identifies the <see cref="CloseButtonText"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonTextProperty = DependencyProperty.Register(
        nameof(CloseButtonText),
        typeof(string),
        typeof(ContentDialog),
        new PropertyMetadata("Close")
    );

    /// <summary>Identifies the <see cref="PrimaryButtonIcon"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryButtonIconProperty = DependencyProperty.Register(
        nameof(PrimaryButtonIcon),
        typeof(IconElement),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="SecondaryButtonIcon"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryButtonIconProperty = DependencyProperty.Register(
        nameof(SecondaryButtonIcon),
        typeof(IconElement),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="CloseButtonIcon"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonIconProperty = DependencyProperty.Register(
        nameof(CloseButtonIcon),
        typeof(IconElement),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="IsPrimaryButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsPrimaryButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsPrimaryButtonEnabled),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="IsSecondaryButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsSecondaryButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsSecondaryButtonEnabled),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="IsCloseButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsCloseButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsCloseButtonEnabled),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="CloseButtonVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonVisibilityProperty = DependencyProperty.Register(
        nameof(CloseButtonVisibility),
        typeof(Visibility),
        typeof(ContentDialog),
        new PropertyMetadata(Visibility.Visible)
    );

    /// <summary>Identifies the <see cref="PrimaryButtonAppearance"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryButtonAppearanceProperty = DependencyProperty.Register(
        nameof(PrimaryButtonAppearance),
        typeof(ControlAppearance),
        typeof(ContentDialog),
        new PropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>Identifies the <see cref="SecondaryButtonAppearance"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryButtonAppearanceProperty = DependencyProperty.Register(
        nameof(SecondaryButtonAppearance),
        typeof(ControlAppearance),
        typeof(ContentDialog),
        new PropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>Identifies the <see cref="CloseButtonAppearance"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonAppearanceProperty = DependencyProperty.Register(
        nameof(CloseButtonAppearance),
        typeof(ControlAppearance),
        typeof(ContentDialog),
        new PropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>Identifies the <see cref="DefaultButton"/> dependency property.</summary>
    public static readonly DependencyProperty DefaultButtonProperty = DependencyProperty.Register(
        nameof(DefaultButton),
        typeof(ContentDialogButton),
        typeof(ContentDialog),
        new PropertyMetadata(ContentDialogButton.Primary)
    );

    /// <summary>Identifies the <see cref="IsFooterVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsFooterVisibleProperty = DependencyProperty.Register(
        nameof(IsFooterVisible),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="TemplateButtonCommand"/> dependency property.</summary>
    public static readonly DependencyProperty TemplateButtonCommandProperty = DependencyProperty.Register(
        nameof(TemplateButtonCommand),
        typeof(IRelayCommand),
        typeof(ContentDialog),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="Opened"/> routed event.</summary>
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<ContentDialog, RoutedEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>Identifies the <see cref="Closing"/> routed event.</summary>
    public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent(
        nameof(Closing),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<ContentDialog, ContentDialogClosingEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>Identifies the <see cref="Closed"/> routed event.</summary>
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<ContentDialog, ContentDialogClosedEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>Identifies the <see cref="ButtonClicked"/> routed event.</summary>
    public static readonly RoutedEvent ButtonClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(ButtonClicked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>),
        typeof(ContentDialog)
    );

    /// <summary>
    /// Gets or sets the title of the <see cref="ContentDialog"/>.
    /// </summary>
    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the title template of the <see cref="ContentDialog"/>.
    /// </summary>
    public DataTemplate? TitleTemplate
    {
        get => (DataTemplate?)GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the <see cref="ContentDialog"/>.
    /// </summary>
    public double DialogWidth
    {
        get => (double)GetValue(DialogWidthProperty);
        set => SetValue(DialogWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the <see cref="ContentDialog"/>.
    /// </summary>
    public double DialogHeight
    {
        get => (double)GetValue(DialogHeightProperty);
        set => SetValue(DialogHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the max width of the <see cref="ContentDialog"/>.
    /// </summary>
    public double DialogMaxWidth
    {
        get => (double)GetValue(DialogMaxWidthProperty);
        set => SetValue(DialogMaxWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the max height of the <see cref="ContentDialog"/>.
    /// </summary>
    public double DialogMaxHeight
    {
        get => (double)GetValue(DialogMaxHeightProperty);
        set => SetValue(DialogMaxHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin of the <see cref="ContentDialog"/>.
    /// </summary>
    public Thickness DialogMargin
    {
        get => (Thickness)GetValue(DialogMarginProperty);
        set => SetValue(DialogMarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the text to display on the primary button.
    /// </summary>
    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the text to be displayed on the secondary button.
    /// </summary>
    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the text to display on the close button.
    /// </summary>
    public string CloseButtonText
    {
        get => (string)GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SymbolRegular"/> on the secondary button.
    /// </summary>
    public IconElement? PrimaryButtonIcon
    {
        get => (IconElement?)GetValue(PrimaryButtonIconProperty);
        set => SetValue(PrimaryButtonIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SymbolRegular"/> on the primary button.
    /// </summary>
    public IconElement? SecondaryButtonIcon
    {
        get => (IconElement?)GetValue(SecondaryButtonIconProperty);
        set => SetValue(SecondaryButtonIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="SymbolRegular"/> on the close button.
    /// </summary>
    public IconElement? CloseButtonIcon
    {
        get => (IconElement?)GetValue(CloseButtonIconProperty);
        set => SetValue(CloseButtonIconProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="ContentDialog"/> primary button is enabled.
    /// </summary>
    public bool IsPrimaryButtonEnabled
    {
        get => (bool)GetValue(IsPrimaryButtonEnabledProperty);
        set => SetValue(IsPrimaryButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="ContentDialog"/> secondary button is enabled.
    /// </summary>
    public bool IsSecondaryButtonEnabled
    {
        get => (bool)GetValue(IsSecondaryButtonEnabledProperty);
        set => SetValue(IsSecondaryButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the close button is enabled.
    /// </summary>
    public bool IsCloseButtonEnabled
    {
        get => (bool)GetValue(IsCloseButtonEnabledProperty);
        set => SetValue(IsCloseButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the visibility of the close button.
    /// </summary>
    public Visibility CloseButtonVisibility
    {
        get => (Visibility)GetValue(CloseButtonVisibilityProperty);
        set => SetValue(CloseButtonVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> to apply to the primary button.
    /// </summary>
    public ControlAppearance PrimaryButtonAppearance
    {
        get => (ControlAppearance)GetValue(PrimaryButtonAppearanceProperty);
        set => SetValue(PrimaryButtonAppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> to apply to the secondary button.
    /// </summary>
    public ControlAppearance SecondaryButtonAppearance
    {
        get => (ControlAppearance)GetValue(SecondaryButtonAppearanceProperty);
        set => SetValue(SecondaryButtonAppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> to apply to the close button.
    /// </summary>
    public ControlAppearance CloseButtonAppearance
    {
        get => (ControlAppearance)GetValue(CloseButtonAppearanceProperty);
        set => SetValue(CloseButtonAppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets a value that indicates which button on the dialog is the default action.
    /// </summary>
    public ContentDialogButton DefaultButton
    {
        get => (ContentDialogButton)GetValue(DefaultButtonProperty);
        set => SetValue(DefaultButtonProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the footer buttons are visible.
    /// </summary>
    public bool IsFooterVisible
    {
        get => (bool)GetValue(IsFooterVisibleProperty);
        set => SetValue(IsFooterVisibleProperty, value);
    }
}
