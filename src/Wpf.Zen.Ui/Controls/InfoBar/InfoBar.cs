// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents an inline notification that participates in the surrounding layout instead of
/// overlaying application content.
/// </summary>
/// <remarks>
/// <para>
/// The notification body is the inherited <see cref="ContentControl.Content"/> slot. It therefore
/// accepts text, arbitrary elements and data objects rendered through
/// <see cref="ContentControl.ContentTemplate"/> in the same way as other WPF content controls.
/// <see cref="Title"/> is also object-valued so templates are not forced to reduce a heading to a
/// string.
/// </para>
/// <para>
/// <see cref="IsIconVisible"/> is the single authority for the semantic icon slot. Disabling it
/// suppresses both the severity-derived default icon and custom <see cref="Icon"/> content, so
/// consumers do not need to clear or restore their icon value merely to change presentation.
/// </para>
/// <para>
/// The notification uses the inherited <see cref="UIElement.Visibility"/> property as its only
/// display state. Activating <c>PART_CloseButton</c> changes the control itself to
/// <see cref="DismissedVisibility"/>, so its margin and other layout properties obey normal WPF
/// visibility semantics. <see cref="IsAutoVisibilityEnabled"/> can additionally derive visibility
/// from whether the title or body contains a value.
/// Dismissal preserves both content slots and does not clear their bound source values.
/// Automatic redisplay requires an effective title or body value change; see
/// <see cref="IsAutoVisibilityEnabled"/> for repeated-message binding guidance.
/// </para>
/// <para>
/// <see cref="IsClosable"/> controls the built-in user-dismiss action. The default template
/// removes the button when it is <see langword="false"/>, and the control also checks the property
/// before accepting a click so a custom template cannot accidentally bypass the contract. A
/// missing or differently typed button part is tolerated and leaves programmatic control through
/// <see cref="UIElement.Visibility"/> intact.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
[StyleTypedProperty(Property = nameof(CloseButtonStyle), StyleTargetType = typeof(ButtonBase))]
[TemplatePart(Name = CloseButtonPartName, Type = typeof(ButtonBase))]
public class InfoBar : ContentControl
{
    private const string CloseButtonPartName = "PART_CloseButton";

    private ButtonBase? _closeButton;

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(InfoBar),
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
        typeof(InfoBar),
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

    #region DP: IsClosable

    /// <summary>Identifies the <see cref="IsClosable"/> dependency property.</summary>
    public static readonly DependencyProperty IsClosableProperty = DependencyProperty.Register(
        nameof(IsClosable),
        typeof(bool),
        typeof(InfoBar),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the built-in close action may dismiss the
    /// notification.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="true"/>. When set to <see langword="false"/>, the
    /// default template removes the close button from layout and the control ignores clicks from a
    /// close-button part retained by a custom template. This property does not prevent application
    /// code from changing <see cref="UIElement.Visibility"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    #endregion

    #region DP: IsAutoVisibilityEnabled

    /// <summary>Identifies the <see cref="IsAutoVisibilityEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsAutoVisibilityEnabledProperty =
        DependencyProperty.Register(
            nameof(IsAutoVisibilityEnabled),
            typeof(bool),
            typeof(InfoBar),
            new PropertyMetadata(true, OnIsAutoVisibilityEnabledChanged)
        );

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="UIElement.Visibility"/> follows the
    /// presence of title or body content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default value is <see langword="true"/>. The control becomes
    /// <see cref="Visibility.Visible"/> when either <see cref="Title"/> or
    /// <see cref="ContentControl.Content"/> has content, and changes to
    /// <see cref="DismissedVisibility"/> when both slots are empty. A slot is empty only when its
    /// value is <see langword="null"/> or an empty <see cref="string"/>; whitespace and non-string
    /// objects remain valid content. Enabling this behavior evaluates the current values
    /// immediately. Disabling it leaves the current visibility unchanged and returns subsequent
    /// visibility control to the application.
    /// </para>
    /// <para>
    /// Automatic visibility is reevaluated when the effective value of either content slot
    /// changes. Assigning the same value does not reliably produce a WPF property-change callback,
    /// and a view-model setter may suppress same-value change notifications entirely. Changing
    /// properties within the existing content object can update its bound presentation without
    /// changing the slot itself, so it does not automatically redisplay a dismissed notification.
    /// </para>
    /// <para>
    /// To display the same text again through a binding, application code can first clear the
    /// source message to null or an empty string, then assign the message again, notifying the
    /// binding of both changes. The cleared value must reach the target before the message is
    /// restored; deferred or asynchronous binding transfers may otherwise observe only the final
    /// value. Clearing is the application's choice: dismissing this control only changes
    /// visibility and never clears its title, content or their bound source values.
    /// </para>
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsAutoVisibilityEnabled
    {
        get => (bool)GetValue(IsAutoVisibilityEnabledProperty);
        set => SetValue(IsAutoVisibilityEnabledProperty, value);
    }

    #endregion

    #region DP: DismissedVisibility

    /// <summary>Identifies the <see cref="DismissedVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty DismissedVisibilityProperty =
        DependencyProperty.Register(
            nameof(DismissedVisibility),
            typeof(InfoBarDismissedVisibility),
            typeof(InfoBar),
            new PropertyMetadata(
                InfoBarDismissedVisibility.Collapsed,
                OnDismissedVisibilityChanged
            ),
            ValidateDismissedVisibility
        );

    /// <summary>
    /// Gets or sets the visibility state used when the notification is dismissed or has no content.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="InfoBarDismissedVisibility.Collapsed"/>, which removes the complete
    /// control, including its margin, from layout.
    /// <see cref="InfoBarDismissedVisibility.Hidden"/> may be selected when the layout slot must be
    /// retained. The dedicated enum exposes only meaningful non-visible states to C#, XAML editors
    /// and property inspectors. Changing the property while the control is already non-visible
    /// applies the new state immediately.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public InfoBarDismissedVisibility DismissedVisibility
    {
        get => (InfoBarDismissedVisibility)GetValue(DismissedVisibilityProperty);
        set => SetValue(DismissedVisibilityProperty, value);
    }

    #endregion

    #region DP: Title

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(InfoBar),
        new PropertyMetadata(null, OnTitleChanged)
    );

    /// <summary>
    /// Gets or sets the optional heading displayed before the notification content.
    /// </summary>
    /// <remarks>
    /// Text is rendered normally, while an element or data object may provide richer heading
    /// content. The default template removes this slot from layout when the value is
    /// <see langword="null"/> or an empty string.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region DP: CornerRadius

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    /// <remarks>
    /// The property reuses WPF's <see cref="Border.CornerRadiusProperty"/>
    /// validation, which rejects negative and non-finite radii. It affects only template rendering
    /// and does not change the control's desired size.
    /// </remarks>
    public static readonly DependencyProperty CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner(
            typeof(InfoBar),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

    /// <summary>
    /// Gets or sets the radius applied to the notification background and border.
    /// </summary>
    /// <value>The radius of each corner. The default is supplied by the active control theme.</value>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: IsIconVisible

    /// <summary>Identifies the <see cref="IsIconVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsIconVisibleProperty = DependencyProperty.Register(
        nameof(IsIconVisible),
        typeof(bool),
        typeof(InfoBar),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the semantic icon slot is shown.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="true"/>. Setting this property to
    /// <see langword="false"/> removes the complete icon slot from the default template, including
    /// its spacing, regardless of whether the slot would otherwise contain the icon selected by
    /// <see cref="Severity"/> or custom <see cref="Icon"/> content. The icon value is retained and
    /// becomes visible again when this property returns to <see langword="true"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public bool IsIconVisible
    {
        get => (bool)GetValue(IsIconVisibleProperty);
        set => SetValue(IsIconVisibleProperty, value);
    }

    #endregion

    #region DP: Icon template

    /// <summary>Identifies the <see cref="IconTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(
        nameof(IconTemplate),
        typeof(DataTemplate),
        typeof(InfoBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the data template used to render <see cref="Icon"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>, allowing WPF to render elements directly and to use
    /// implicit data templates for model objects. This property has no effect while
    /// <see cref="Icon"/> is <see langword="null"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public DataTemplate? IconTemplate
    {
        get => (DataTemplate?)GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    #endregion

    #region DP: Close button appearance

    /// <summary>Identifies the <see cref="CloseButtonStyle"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonStyleProperty = DependencyProperty.Register(
        nameof(CloseButtonStyle),
        typeof(Style),
        typeof(InfoBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the style applied to <c>PART_CloseButton</c>.
    /// </summary>
    /// <remarks>
    /// This property provides a supported way to change the close glyph, spacing, colors or focus
    /// visuals without copying the complete <see cref="ControlTemplate"/>. The control binds the
    /// property to the named template part when the template is applied, so custom templates only
    /// need to preserve the documented part name and do not need to repeat that binding.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Style? CloseButtonStyle
    {
        get => (Style?)GetValue(CloseButtonStyleProperty);
        set => SetValue(CloseButtonStyleProperty, value);
    }

    #endregion

    #region DP: Severity

    /// <summary>Identifies the <see cref="Severity"/> dependency property.</summary>
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(InfoBarSeverity),
        typeof(InfoBar),
        new PropertyMetadata(InfoBarSeverity.Informational)
    );

    /// <summary>
    /// Gets or sets the semantic severity used by the template to select the notification icon and
    /// status colors.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="InfoBarSeverity.Informational"/>. Changing this property
    /// affects rendering but does not alter content, layout state or close behavior.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public InfoBarSeverity Severity
    {
        get => (InfoBarSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    #endregion

    static InfoBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(InfoBar),
            new FrameworkPropertyMetadata(typeof(InfoBar))
        );

        // Auto visibility is enabled by default, so an empty InfoBar must not briefly reserve
        // layout space before its title or content is assigned. Overriding the inherited default
        // also avoids installing a local Visibility value, preserving normal style and binding
        // precedence for applications that disable automatic management.
        VisibilityProperty.OverrideMetadata(
            typeof(InfoBar),
            new FrameworkPropertyMetadata(Visibility.Collapsed)
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// ContentControl's callback has already updated the logical tree before this override runs.
    /// Evaluating the content-driven state contract here keeps inherited
    /// <see cref="ContentControl.Content"/> semantics intact while ensuring bindings, styles and
    /// direct assignments all use the same visibility path.
    /// </remarks>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        UpdateVisibilityFromContent();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Template parts are event sources owned by a replaceable visual tree. The previous handler
    /// is removed before resolving the new part so repeated template application cannot retain an
    /// obsolete button or execute the close transition more than once.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachCloseButton();
        base.OnApplyTemplate();

        _closeButton = GetTemplateChild(CloseButtonPartName) as ButtonBase;
        if (_closeButton is not null)
        {
            // CloseButtonStyle is a control contract rather than a convention of the default
            // template. WPF uses the same owner-to-child binding pattern for properties such as
            // DatePicker.CalendarStyle and Calendar.CalendarButtonStyle. Installing the binding
            // here also keeps later style, resource and binding updates synchronized when a
            // custom template provides the part but does not explicitly forward the property.
            BindingOperations.SetBinding(
                _closeButton,
                FrameworkElement.StyleProperty,
                new Binding(nameof(CloseButtonStyle)) { Source = this }
            );

            _closeButton.Click += OnCloseButtonClick;
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // Template replacement need not be followed by an immediate layout pass. Detaching here
        // prevents a retained old visual from changing Visibility while the new template is pending.
        DetachCloseButton();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsClosable)
        {
            // The control itself must become non-visible so its margin follows WPF's standard
            // layout behavior. SetCurrentValue retains a binding on Visibility; an explicitly
            // two-way binding can therefore observe user dismissal without losing the expression.
            SetCurrentValue(VisibilityProperty, GetDismissedVisibility());
        }

        // The button is an implementation detail rather than part of InfoBar's public event
        // surface. Always consume its click, including the rejected IsClosable=false path, so a
        // custom template cannot turn the private part into an accidental application-level event.
        e.Handled = true;
    }

    private static void OnIsAutoVisibilityEnabledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if ((bool)e.NewValue)
        {
            ((InfoBar)d).UpdateVisibilityFromContent();
        }
    }

    private static void OnDismissedVisibilityChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        var infoBar = (InfoBar)d;
        if (infoBar.Visibility != Visibility.Visible)
        {
            // Hidden and Collapsed are semantically the same dismissed state for InfoBar. Apply
            // the new layout policy immediately without requiring another content transition.
            infoBar.SetCurrentValue(VisibilityProperty, infoBar.GetDismissedVisibility());
        }
    }

    private static void OnTitleChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((InfoBar)d).UpdateVisibilityFromContent();
    }

    private void UpdateVisibilityFromContent()
    {
        if (!IsAutoVisibilityEnabled)
        {
            return;
        }

        var visibility =
            !IsEmptyContentValue(Title) || !IsEmptyContentValue(Content)
                ? Visibility.Visible
                : GetDismissedVisibility();
        if (Visibility == visibility)
        {
            return;
        }

        // Content-driven behavior and application control intentionally share the inherited
        // property. SetCurrentValue changes its effective value without discarding an existing
        // binding, allowing the application to resume control when automatic management is off.
        SetCurrentValue(VisibilityProperty, visibility);
    }

    private static bool ValidateDismissedVisibility(object? value)
    {
        return value is InfoBarDismissedVisibility.Hidden or InfoBarDismissedVisibility.Collapsed;
    }

    private Visibility GetDismissedVisibility()
    {
        // The public enum intentionally models the policy rather than reusing WPF's three-state
        // Visibility type. Keep the conversion at the point where the inherited property is
        // updated so the rest of the control continues to use normal WPF layout semantics.
        return DismissedVisibility switch
        {
            InfoBarDismissedVisibility.Hidden => Visibility.Hidden,
            InfoBarDismissedVisibility.Collapsed => Visibility.Collapsed,
            _ => throw new InvalidOperationException(
                $"Unsupported dismissed visibility value: {DismissedVisibility}."
            ),
        };
    }

    private static bool IsEmptyContentValue(object? value)
    {
        return value is null or string { Length: 0 };
    }

    private void DetachCloseButton()
    {
        if (_closeButton is null)
        {
            return;
        }

        _closeButton.Click -= OnCloseButtonClick;
        _closeButton = null;
    }
}
