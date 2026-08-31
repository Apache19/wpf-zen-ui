// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* TODO: Refactor as popup, detach from the window renderer */

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Snackbar inform user of a process that an app has performed or will perform. It appears temporarily, towards the bottom of the window.
/// </summary>
[TemplatePart(Name = CloseButtonPartName, Type = typeof(ButtonBase))]
public class Snackbar : ContentControl
{
    private const string CloseButtonPartName = "PART_CloseButton";

    private ButtonBase? _closeButton;

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(Snackbar),
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
        typeof(Snackbar),
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

    /// <summary>Identifies the <see cref="IsCloseButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsCloseButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsCloseButtonEnabled),
        typeof(bool),
        typeof(Snackbar),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="SlideTransform"/> dependency property.</summary>
    public static readonly DependencyProperty SlideTransformProperty = DependencyProperty.Register(
        nameof(SlideTransform),
        typeof(TranslateTransform),
        typeof(Snackbar),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="IsShown"/> dependency property.</summary>
    public static readonly DependencyProperty IsShownProperty = DependencyProperty.Register(
        nameof(IsShown),
        typeof(bool),
        typeof(Snackbar),
        new PropertyMetadata(false, (d, e) => (d as Snackbar)?.OnIsShownChanged(e))
    );

    /// <summary>Identifies the <see cref="Timeout"/> dependency property.</summary>
    public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(
        nameof(Timeout),
        typeof(TimeSpan),
        typeof(Snackbar),
        new PropertyMetadata(TimeSpan.FromSeconds(2))
    );

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(Snackbar),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="TitleTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty TitleTemplateProperty = DependencyProperty.Register(
        nameof(TitleTemplate),
        typeof(DataTemplate),
        typeof(Snackbar),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(ControlAppearance),
        typeof(Snackbar),
        new PropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>Identifies the <see cref="ContentForeground"/> dependency property.</summary>
    public static readonly DependencyProperty ContentForegroundProperty = DependencyProperty.Register(
        nameof(ContentForeground),
        typeof(Brush),
        typeof(Snackbar),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>Identifies the <see cref="Opened"/> routed event.</summary>
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<Snackbar, RoutedEventArgs>),
        typeof(Snackbar)
    );

    /// <summary>Identifies the <see cref="Closed"/> routed event.</summary>
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<Snackbar, RoutedEventArgs>),
        typeof(Snackbar)
    );

    static Snackbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Snackbar),
            new FrameworkPropertyMetadata(typeof(Snackbar))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="Snackbar"/> close button should be visible.
    /// </summary>
    public bool IsCloseButtonEnabled
    {
        get => (bool)GetValue(IsCloseButtonEnabledProperty);
        set => SetValue(IsCloseButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the transform.
    /// </summary>
    public TranslateTransform? SlideTransform
    {
        get => (TranslateTransform?)GetValue(SlideTransformProperty);
        set => SetValue(SlideTransformProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="Snackbar"/> is visible.
    /// </summary>
    public bool IsShown
    {
        get => (bool)GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    protected void OnIsShownChanged(DependencyPropertyChangedEventArgs e)
    {
        bool newValue = (bool)e.NewValue;

        if (newValue)
        {
            OnOpened();
        }
        else
        {
            OnClosed();
        }
    }

    /// <summary>
    /// Gets or sets a time for which the <see cref="Snackbar"/> should be visible.
    /// </summary>
    public TimeSpan Timeout
    {
        get => (TimeSpan)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the title of the <see cref="Snackbar"/>.
    /// </summary>
    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the title template of the <see cref="Snackbar"/>.
    /// </summary>
    public DataTemplate? TitleTemplate
    {
        get => (DataTemplate?)GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> of the snackbar.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public ControlAppearance Appearance
    {
        get => (ControlAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground of the <see cref="ContentControl.Content"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush ContentForeground
    {
        get => (Brush)GetValue(ContentForegroundProperty);
        set => SetValue(ContentForegroundProperty, value);
    }

    /// <summary>
    /// Occurs when the snackbar is about to open.
    /// </summary>
    public event TypedEventHandler<Snackbar, RoutedEventArgs> Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>
    /// Occurs when the snackbar is about to close.
    /// </summary>
    public event TypedEventHandler<Snackbar, RoutedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Snackbar"/> class with a specified presenter.
    /// </summary>
    /// <param name="presenter">The <see cref="SnackbarPresenter"/> to manage the snackbar's display and interactions.</param>
    public Snackbar(SnackbarPresenter presenter)
    {
        Presenter = presenter;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The close button is an optional template part. The handler follows the applied visual tree
    /// rather than being exposed as a command, so custom templates can omit the button without
    /// affecting queue-driven display and dismissal.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachCloseButton();
        base.OnApplyTemplate();

        _closeButton = GetTemplateChild(CloseButtonPartName) as ButtonBase;
        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseButtonClick;
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // Detach at assignment time because applying the replacement can be deferred until layout.
        DetachCloseButton();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    protected SnackbarPresenter Presenter { get; }

    /// <summary>
    /// Shows the <see cref="Snackbar"/>
    /// </summary>
    public virtual void Show()
    {
        Show(false);
    }

    /// <summary>
    /// Shows the <see cref="Snackbar"/>
    /// </summary>
    public virtual void Show(bool immediately)
    {
        if (immediately)
        {
            _ = Presenter.ImmediatelyDisplay(this);
        }
        else
        {
            Presenter.AddToQueue(this);
        }
    }

    /// <summary>
    /// Shows the <see cref="Snackbar"/>.
    /// </summary>
    public virtual Task ShowAsync()
    {
        return ShowAsync(false);
    }

    /// <summary>
    /// Shows the <see cref="Snackbar"/>.
    /// </summary>
    public virtual async Task ShowAsync(bool immediately)
    {
        if (immediately)
        {
            await Presenter.ImmediatelyDisplay(this);
        }
        else
        {
            Presenter.AddToQueue(this);
        }
    }

    /// <summary>
    /// Hides the <see cref="Snackbar"/>
    /// </summary>
    protected virtual void Hide()
    {
        _ = Presenter.HideCurrent();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Hide();
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

    /// <summary>
    /// This virtual method is called when <see cref="Snackbar"/> is opening and it raises the <see cref="Opened"/> <see langword="event"/>.
    /// </summary>
    protected virtual void OnOpened()
    {
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    /// <summary>
    /// This virtual method is called when <see cref="Snackbar"/> is closing and it raises the <see cref="Closed"/> <see langword="event"/>.
    /// </summary>
    protected virtual void OnClosed()
    {
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }
}
