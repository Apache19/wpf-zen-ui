// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
/// suppresses both the severity-derived default icon and custom
/// <see cref="IconAssist.IconProperty"/> content, so
/// consumers do not need to clear or restore their icon value merely to change presentation.
/// </para>
/// <para>
/// <see cref="IsOpen"/> is the notification's logical state; the default template collapses its
/// internal root when that state is <see langword="false"/>. It deliberately does not modify
/// <see cref="UIElement.Visibility"/>, which remains an independent, application-owned layout
/// concern. Activating <c>PART_CloseButton</c> and the content-driven behavior controlled by
/// <see cref="IsOpenAutoManaged"/> change that same state rather than introducing additional state
/// mechanisms.
/// </para>
/// <para>
/// <see cref="IsClosable"/> controls the built-in user-dismiss action. The default template
/// removes the button when it is <see langword="false"/>, and the control also checks the property
/// before accepting a click so a custom template cannot accidentally bypass the contract. A
/// missing or differently typed button part is tolerated and leaves programmatic control through
/// <see cref="IsOpen"/> intact.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
[StyleTypedProperty(Property = nameof(CloseButtonStyle), StyleTargetType = typeof(ButtonBase))]
[TemplatePart(Name = CloseButtonPartName, Type = typeof(ButtonBase))]
public class InfoBar : ContentControl
{
    private const string CloseButtonPartName = "PART_CloseButton";

    private ButtonBase? _closeButton;

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
    /// code from changing <see cref="IsOpen"/> or <see cref="UIElement.Visibility"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    #endregion

    #region DP: IsOpenAutoManaged

    /// <summary>Identifies the <see cref="IsOpenAutoManaged"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenAutoManagedProperty =
        DependencyProperty.Register(
            nameof(IsOpenAutoManaged),
            typeof(bool),
            typeof(InfoBar),
            new PropertyMetadata(true, OnIsOpenAutoManagedChanged)
        );

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="IsOpen"/> follows the presence of title
    /// or body content.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="true"/>. The notification opens when either
    /// <see cref="Title"/> or <see cref="ContentControl.Content"/> has content, and closes when both
    /// slots are empty. A slot is empty only when its value is <see langword="null"/> or an empty
    /// <see cref="string"/>; whitespace and non-string objects remain valid content. Enabling this
    /// behavior evaluates the current values immediately. Automatic transitions update
    /// <see cref="IsOpen"/> through its current value so an existing binding is retained and, for
    /// the default two-way binding mode, receives the new state.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsOpenAutoManaged
    {
        get => (bool)GetValue(IsOpenAutoManagedProperty);
        set => SetValue(IsOpenAutoManagedProperty, value);
    }

    #endregion

    #region DP: IsOpen

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the notification is logically open.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. The default template uses this property only
    /// to show or collapse its internal presentation; it never overwrites
    /// <see cref="UIElement.Visibility"/>. The property binds two-way by default so a close-button
    /// activation can update a view-model without replacing the binding expression.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
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
    /// <see cref="Severity"/> or custom <see cref="IconAssist.IconProperty"/> content. The icon
    /// value is retained and
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
    /// Gets or sets the data template used to render <see cref="IconAssist.IconProperty"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>, allowing WPF to render elements directly and to use
    /// implicit data templates for model objects. This property has no effect while
    /// <see cref="IconAssist.IconProperty"/> is <see langword="null"/>.
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
    /// Gets or sets the style applied to <c>PART_CloseButton</c> by the default template.
    /// </summary>
    /// <remarks>
    /// This property provides a supported way to change the close glyph, spacing, colors or focus
    /// visuals without copying the complete <see cref="ControlTemplate"/>. A custom template may
    /// choose whether to consume it.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Style? CloseButtonStyle
    {
        get => (Style?)GetValue(CloseButtonStyleProperty);
        set => SetValue(CloseButtonStyleProperty, value);
    }

    /// <summary>Identifies the <see cref="CloseButtonToolTip"/> dependency property.</summary>
    public static readonly DependencyProperty CloseButtonToolTipProperty = DependencyProperty.Register(
        nameof(CloseButtonToolTip),
        typeof(object),
        typeof(InfoBar),
        new PropertyMetadata("Close")
    );

    /// <summary>
    /// Gets or sets the tooltip content shown for the built-in close button.
    /// </summary>
    /// <remarks>
    /// The default value is <c>Close</c>. The value is object-based so applications can provide
    /// localized text or rich tooltip content without replacing the control template.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    [Localizability(LocalizationCategory.Text)]
    public object? CloseButtonToolTip
    {
        get => GetValue(CloseButtonToolTipProperty);
        set => SetValue(CloseButtonToolTipProperty, value);
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
    }

    /// <inheritdoc />
    /// <remarks>
    /// ContentControl's callback has already updated the logical tree before this override runs.
    /// Evaluating the content-driven state contract here keeps inherited
    /// <see cref="ContentControl.Content"/> semantics intact while ensuring bindings, styles and
    /// direct assignments all use the same close path.
    /// </remarks>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        UpdateIsOpenFromContent();
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
        _closeButton?.Click += OnCloseButtonClick;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // Template replacement need not be followed by an immediate layout pass. Detaching here
        // prevents a retained old visual from changing IsOpen while the new template is pending.
        DetachCloseButton();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsClosable)
        {
            // SetCurrentValue preserves a consumer's binding. IsOpen is the only state transition:
            // Visibility remains untouched even when the default template stops presenting the bar.
            SetCurrentValue(IsOpenProperty, false);
        }

        // The button is an implementation detail rather than part of InfoBar's public event
        // surface. Always consume its click, including the rejected IsClosable=false path, so a
        // custom template cannot turn the private part into an accidental application-level event.
        e.Handled = true;
    }

    private static void OnIsOpenAutoManagedChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if ((bool)e.NewValue)
        {
            ((InfoBar)d).UpdateIsOpenFromContent();
        }
    }

    private static void OnTitleChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((InfoBar)d).UpdateIsOpenFromContent();
    }

    private void UpdateIsOpenFromContent()
    {
        if (!IsOpenAutoManaged)
        {
            return;
        }

        var shouldOpen = !IsEmptyContentValue(Title) || !IsEmptyContentValue(Content);
        if (IsOpen == shouldOpen)
        {
            return;
        }

        // SetCurrentValue retains bindings and lets IsOpen's two-way-by-default metadata publish
        // either transition instead of replacing the application's binding with a local value.
        SetCurrentValue(IsOpenProperty, shouldOpen);
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
