// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines logical footer presentation, initial focus and the close lifecycle contract.
/// </summary>
public partial class ContentDialog
{
    #region DP: CornerRadius

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The property reuses WPF's <see cref="Border.CornerRadiusProperty"/> validation, which
    /// rejects negative and non-finite radii. It affects the visual shell through template
    /// bindings and does not otherwise change the dialog's desired size.
    /// </remarks>
    public static readonly DependencyProperty CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

    /// <summary>
    /// Gets or sets the radius applied to the standard dialog shell and its footer background.
    /// The default is supplied by the active control theme.
    /// </summary>
    /// <remarks>
    /// The default template copies the bottom two radii to the footer so its background follows
    /// the outer shell. A replacement template may consume this property differently.
    /// </remarks>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: SizingMode

    /// <summary>
    /// Identifies the <see cref="SizingMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SizingModeProperty = DependencyProperty.Register(
        nameof(SizingMode),
        typeof(ContentDialogSizingMode),
        typeof(ContentDialog),
        new FrameworkPropertyMetadata(ContentDialogSizingMode.Auto),
        static value => value is ContentDialogSizingMode.Auto
            or ContentDialogSizingMode.Stretch
            or ContentDialogSizingMode.StretchWidth
            or ContentDialogSizingMode.StretchHeight
    );

    /// <summary>
    /// Gets or sets which dimensions of the dialog consume the space available inside the host.
    /// The default is <see cref="ContentDialogSizingMode.Auto"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default template keeps its outer host padding in every mode. Stretching therefore fills
    /// the usable dialog area rather than covering the host edge-to-edge.
    /// </para>
    /// <para>
    /// The default theme limits automatic width to 640 and automatic height to 850
    /// device-independent pixels. Each default limit applies only when the corresponding dimension
    /// is Auto and its axis is not stretched. Setting Width or Height releases that axis's default
    /// limit; restoring Auto reapplies it. Application-supplied maximums retain their normal WPF
    /// property-value precedence and continue to constrain explicit and stretched dimensions.
    /// </para>
    /// <para>
    /// Custom <see cref="ContentDialog"/> and <see cref="ContentDialogTransitionPresenter"/>
    /// templates should consume this property when they replace the standard sizing surfaces.
    /// </para>
    /// </remarks>
    public ContentDialogSizingMode SizingMode
    {
        get => (ContentDialogSizingMode)GetValue(SizingModeProperty);
        set => SetValue(SizingModeProperty, value);
    }

    #endregion

    #region DP: HeaderPadding

    /// <summary>
    /// Identifies the <see cref="HeaderPadding"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HeaderPaddingProperty = DependencyProperty.Register(
        nameof(HeaderPadding),
        typeof(Thickness),
        typeof(ContentDialog),
        new FrameworkPropertyMetadata(
            new Thickness(0),
            FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <summary>
    /// Gets or sets the space between the standard dialog shell and the header content. The default
    /// is 24 pixels on the left, 20 on top, 48 on the right, and 8 on the bottom.
    /// </summary>
    /// <remarks>
    /// The larger right inset reserves the independent top-right dismiss surface without vertically
    /// aligning arbitrary header content to that button. This value affects layout, does not inherit,
    /// can be animated, and is consumed only by templates that bind it.
    /// </remarks>
    public Thickness HeaderPadding
    {
        get => (Thickness)GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    #endregion

    #region DP: FooterPadding

    /// <summary>
    /// Identifies the <see cref="FooterPadding"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterPaddingProperty = DependencyProperty.Register(
        nameof(FooterPadding),
        typeof(Thickness),
        typeof(ContentDialog),
        new FrameworkPropertyMetadata(
            new Thickness(0),
            FrameworkPropertyMetadataOptions.AffectsMeasure
        )
    );

    /// <summary>
    /// Gets or sets the space between the standard footer chrome and <see cref="Footer"/>. The
    /// default is 20 pixels on every side.
    /// </summary>
    /// <remarks>
    /// Footer content owns only its internal composition; this property lets the dialog template
    /// consistently own the surrounding footer spacing. It affects layout, does not inherit, can
    /// be animated, and is consumed only by templates that bind it.
    /// </remarks>
    public Thickness FooterPadding
    {
        get => (Thickness)GetValue(FooterPaddingProperty);
        set => SetValue(FooterPaddingProperty, value);
    }

    #endregion

    #region DP: Footer

    /// <summary>
    /// Identifies the <see cref="Footer"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(
            nameof(Footer),
            typeof(object),
            typeof(ContentDialog),
            new PropertyMetadata(
                null,
                static (d, e) => ((ContentDialog)d).OnFooterChanged(e.OldValue, e.NewValue)
            )
        );

    /// <summary>
    /// Gets or sets the footer content. Default null.
    /// Maintains logical ownership for binding and resources.
    /// </summary>
    /// <remarks>
    /// Not inherited. Presentation properties update layout through template bindings;
    /// no direct measure/render flags or coercion are applied.
    /// Discrete animation values follow the same callbacks.
    /// </remarks>
    public object? Footer
    {
        get => (object?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    #endregion

    #region DP: FooterTemplate

    /// <summary>
    /// Identifies the <see cref="FooterTemplate"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterTemplateProperty =
        DependencyProperty.Register(
            nameof(FooterTemplate),
            typeof(DataTemplate),
            typeof(ContentDialog),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the footer template. Default null uses implicit templates;
    /// an explicit template takes precedence over the selector.
    /// </summary>
    /// <remarks>
    /// Not inherited. Presentation properties update layout through template bindings;
    /// no direct measure/render flags or coercion are applied.
    /// Discrete animation values follow the same callbacks.
    /// </remarks>
    public DataTemplate? FooterTemplate
    {
        get => (DataTemplate?)GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    #endregion

    #region DP: FooterTemplateSelector

    /// <summary>
    /// Identifies the <see cref="FooterTemplateSelector"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(FooterTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ContentDialog),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the footer template selector. Default null uses WPF implicit template lookup.
    /// </summary>
    /// <remarks>
    /// Not inherited. Presentation properties update layout through template bindings;
    /// no direct measure/render flags or coercion are applied.
    /// Discrete animation values follow the same callbacks.
    /// </remarks>
    public DataTemplateSelector? FooterTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(FooterTemplateSelectorProperty);
        set => SetValue(FooterTemplateSelectorProperty, value);
    }

    #endregion

    #region DP: FooterStringFormat

    /// <summary>
    /// Identifies the <see cref="FooterStringFormat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FooterStringFormatProperty =
        DependencyProperty.Register(
            nameof(FooterStringFormat),
            typeof(string),
            typeof(ContentDialog),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the default text format for Footer. Default null;
    /// UIElement content and explicit templates own their formatting.
    /// </summary>
    /// <remarks>
    /// Not inherited. Presentation properties update layout through template bindings;
    /// no direct measure/render flags or coercion are applied.
    /// Discrete animation values follow the same callbacks.
    /// </remarks>
    public string? FooterStringFormat
    {
        get => (string?)GetValue(FooterStringFormatProperty);
        set => SetValue(FooterStringFormatProperty, value);
    }

    #endregion

    #region DP: InitialFocus

    /// <summary>
    /// Identifies the <see cref="InitialFocus"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InitialFocusProperty =
        DependencyProperty.Register(
            nameof(InitialFocus),
            typeof(IInputElement),
            typeof(ContentDialog),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the initial focus target. Default null searches body content, skipping Danger and Caution buttons.
    /// An explicit target may select these buttons, elements outside the Tab order, or focusable 3D elements.
    /// Targets outside the dialog or unavailable targets are ignored.
    /// </summary>
    /// <remarks>
    /// Not inherited or coerced. This target is read during focus initialization only;
    /// changing it while open does not move focus or affect layout/rendering.
    /// Discrete animation can change the stored target.
    /// </remarks>
    public IInputElement? InitialFocus
    {
        get => (IInputElement?)GetValue(InitialFocusProperty);
        set => SetValue(InitialFocusProperty, value);
    }

    #endregion

    #region DP: IsDismissible

    /// <summary>
    /// Identifies the <see cref="IsDismissible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsDismissibleProperty =
        DependencyProperty.Register(
            nameof(IsDismissible),
            typeof(bool),
            typeof(ContentDialog),
            new PropertyMetadata(
                true,
                static (_, _) => CommandManager.InvalidateRequerySuggested()
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether user cancellation is allowed. Default true.
    /// Does not prevent Close(result), token cancellation or host cleanup.
    /// </summary>
    /// <remarks>
    /// Not inherited or coerced. The change callback invalidates command availability;
    /// the template updates the dismiss button visibility. Boolean key-frame animations use the same policy.
    /// </remarks>
    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    #endregion

    #region DP: IsOpen

    private static readonly DependencyPropertyKey IsOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsOpen),
            typeof(bool),
            typeof(ContentDialog),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = IsOpenPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether a presentation is reserved, including entrance, exit and Closed notification.
    /// Default false; this state does not affect layout.
    /// </summary>
    public bool IsOpen => (bool)GetValue(IsOpenProperty);

    #endregion

    #region DP: IsClosing

    private static readonly DependencyPropertyKey IsClosingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsClosing),
        typeof(bool),
        typeof(ContentDialog),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="IsClosing"/> dependency property.</summary>
    public static readonly DependencyProperty IsClosingProperty = IsClosingPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether a close result has been accepted and presentation cleanup is pending. Default false; this state does not affect layout.</summary>
    public bool IsClosing => (bool)GetValue(IsClosingProperty);

    #endregion

    #region EVENT: Closing

    /// <summary>
    /// Identifies the <see cref="Closing"/> routed event.
    /// </summary>
    public static readonly RoutedEvent ClosingEvent =
        EventManager.RegisterRoutedEvent(
            nameof(Closing),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ContentDialogClosingEventArgs>),
            typeof(ContentDialog)
        );

    /// <summary>
    /// Occurs before an ordinary close is accepted. Forced cleanup and cancellation cannot be vetoed.
    /// </summary>
    public event EventHandler<ContentDialogClosingEventArgs> Closing
    {
        add => AddHandler(ClosingEvent, value);
        remove => RemoveHandler(ClosingEvent, value);
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

    /// <summary>
    /// Occurs once after the dialog is loaded and its entrance transition completes.
    /// A dialog dismissed during entrance does not raise this event.
    /// </summary>
    public event RoutedEventHandler Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    #endregion

    #region EVENT: Closed

    /// <summary>Identifies the <see cref="Closed"/> routed event.</summary>
    public static readonly RoutedEvent ClosedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(Closed),
            RoutingStrategy.Direct,
            typeof(EventHandler<ContentDialogClosedEventArgs>),
            typeof(ContentDialog)
        );

    /// <summary>
    /// Occurs once after presentation cleanup. This direct notification cannot cancel closure.
    /// Without an accepted choice, removal and cancellation report null.
    /// </summary>
    /// <remarks>
    /// Subscribe on the dialog itself; its parent route no longer exists after removal.
    /// </remarks>
    public event EventHandler<ContentDialogClosedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion
}
