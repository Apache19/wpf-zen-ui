// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Defines logical footer presentation, initial focus and the close lifecycle contract.</summary>
public partial class ContentDialog
{
    /// <summary>Identifies the <see cref="Footer"/> dependency property.</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(ContentDialog), new PropertyMetadata(null, (d, e) => ((ContentDialog)d).OnFooterChanged(e.OldValue, e.NewValue)));

    /// <summary>Gets or sets the footer content. Default null. Maintains logical ownership for binding and resources.</summary>
    /// <remarks>Not inherited. Presentation properties update layout through template bindings; no direct measure/render flags or coercion are applied. Discrete animation values follow the same callbacks.</remarks>
    public object? Footer
    {
        get => (object?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>Identifies the <see cref="FooterTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty FooterTemplateProperty = DependencyProperty.Register(
        nameof(FooterTemplate), typeof(DataTemplate), typeof(ContentDialog), new PropertyMetadata(null));

    /// <summary>Gets or sets the footer template. Default null uses implicit templates; an explicit template takes precedence over the selector.</summary>
    /// <remarks>Not inherited. Presentation properties update layout through template bindings; no direct measure/render flags or coercion are applied. Discrete animation values follow the same callbacks.</remarks>
    public DataTemplate? FooterTemplate
    {
        get => (DataTemplate?)GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    /// <summary>Identifies the <see cref="FooterTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty FooterTemplateSelectorProperty = DependencyProperty.Register(
        nameof(FooterTemplateSelector), typeof(DataTemplateSelector), typeof(ContentDialog), new PropertyMetadata(null));

    /// <summary>Gets or sets the footer template selector. Default null uses WPF implicit template lookup.</summary>
    /// <remarks>Not inherited. Presentation properties update layout through template bindings; no direct measure/render flags or coercion are applied. Discrete animation values follow the same callbacks.</remarks>
    public DataTemplateSelector? FooterTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(FooterTemplateSelectorProperty);
        set => SetValue(FooterTemplateSelectorProperty, value);
    }

    /// <summary>Identifies the <see cref="FooterStringFormat"/> dependency property.</summary>
    public static readonly DependencyProperty FooterStringFormatProperty = DependencyProperty.Register(
        nameof(FooterStringFormat), typeof(string), typeof(ContentDialog), new PropertyMetadata(null));

    /// <summary>Gets or sets the default text format for Footer. Default null; UIElement content and explicit templates own their formatting.</summary>
    /// <remarks>Not inherited. Presentation properties update layout through template bindings; no direct measure/render flags or coercion are applied. Discrete animation values follow the same callbacks.</remarks>
    public string? FooterStringFormat
    {
        get => (string?)GetValue(FooterStringFormatProperty);
        set => SetValue(FooterStringFormatProperty, value);
    }

    /// <summary>Identifies the <see cref="InitialFocus"/> dependency property.</summary>
    public static readonly DependencyProperty InitialFocusProperty = DependencyProperty.Register(
        nameof(InitialFocus), typeof(IInputElement), typeof(ContentDialog), new PropertyMetadata(null));

    /// <summary>Gets or sets the initial focus target. Default null searches body content. Targets outside the dialog or unavailable targets are ignored.</summary>
    /// <remarks>Not inherited or coerced. This target is read during focus initialization only; changing it while open does not move focus or affect layout/rendering. Discrete animation can change the stored target.</remarks>
    public IInputElement? InitialFocus
    {
        get => (IInputElement?)GetValue(InitialFocusProperty);
        set => SetValue(InitialFocusProperty, value);
    }

    /// <summary>Identifies the <see cref="IsDismissible"/> dependency property.</summary>
    public static readonly DependencyProperty IsDismissibleProperty = DependencyProperty.Register(
        nameof(IsDismissible), typeof(bool), typeof(ContentDialog), new PropertyMetadata(true, (d, e) => CommandManager.InvalidateRequerySuggested()));

    /// <summary>Gets or sets a value indicating whether user cancellation is allowed. Default true. Does not prevent Close(result), token cancellation or host cleanup.</summary>
    /// <remarks>Not inherited or coerced. The change callback invalidates command availability; the template updates the dismiss button visibility. Boolean key-frame animations use the same policy.</remarks>
    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <summary>Identifies the synchronous Closing veto event.</summary>
    public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent(
        nameof(Closing), RoutingStrategy.Bubble, typeof(EventHandler<ContentDialogClosingEventArgs>), typeof(ContentDialog));

    /// <summary>Occurs before an ordinary close is accepted. Forced cleanup and cancellation cannot be vetoed.</summary>
    public event EventHandler<ContentDialogClosingEventArgs> Closing
    {
        add => AddHandler(ClosingEvent, value);
        remove => RemoveHandler(ClosingEvent, value);
    }

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

    /// <summary>Occurs once after presentation cleanup. This direct notification cannot cancel closure. Without an accepted choice, removal and cancellation report null.</summary>
    /// <remarks>Subscribe on the dialog itself; its parent route no longer exists after removal.</remarks>
    public event EventHandler<ContentDialogClosedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
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