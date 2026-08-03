// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ComboBox"/> with interaction-state brushes and a
/// control-owned <see cref="CornerRadius"/>, allowing the closed control surface to be restyled
/// without replacing its template.
/// </summary>
/// <remarks>
/// <para>
/// The normal appearance continues to use <see cref="Control.Background"/>,
/// <see cref="Control.BorderBrush"/>, and <see cref="Control.Foreground"/>. State-specific
/// brushes are optional; a <see langword="null"/> state brush falls back to the corresponding
/// normal brush.
/// </para>
/// <para>
/// Editable combo boxes use the focused brushes while keyboard focus is within the embedded
/// editor. Non-editable combo boxes use the drop-down-open brushes while their popup is open.
/// <see cref="System.Windows.Controls.ComboBox.IsReadOnly"/> only affects the editable editor,
/// so a read-only editable combo box intentionally remains in the editable focus-state path.
/// </para>
/// </remarks>
public class ComboBox : System.Windows.Controls.ComboBox
{
    #region Interaction appearance

    /// <summary>Identifies the <see cref="MouseOverForeground"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverForegroundProperty =
        DependencyProperty.Register(
            nameof(MouseOverForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverForeground
    {
        get => (Brush?)GetValue(MouseOverForegroundProperty);
        set => SetValue(MouseOverForegroundProperty, value);
    }

    /// <summary>Identifies the <see cref="MouseOverBackground"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverBackgroundProperty =
        DependencyProperty.Register(
            nameof(MouseOverBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBackground
    {
        get => (Brush?)GetValue(MouseOverBackgroundProperty);
        set => SetValue(MouseOverBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="MouseOverBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverBorderBrushProperty =
        DependencyProperty.Register(
            nameof(MouseOverBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBorderBrush
    {
        get => (Brush?)GetValue(MouseOverBorderBrushProperty);
        set => SetValue(MouseOverBorderBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="FocusedForeground"/> dependency property.</summary>
    public static readonly DependencyProperty FocusedForegroundProperty =
        DependencyProperty.Register(
            nameof(FocusedForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedForeground
    {
        get => (Brush?)GetValue(FocusedForegroundProperty);
        set => SetValue(FocusedForegroundProperty, value);
    }

    /// <summary>Identifies the <see cref="FocusedBackground"/> dependency property.</summary>
    public static readonly DependencyProperty FocusedBackgroundProperty =
        DependencyProperty.Register(
            nameof(FocusedBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBackground
    {
        get => (Brush?)GetValue(FocusedBackgroundProperty);
        set => SetValue(FocusedBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="FocusedBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty FocusedBorderBrushProperty =
        DependencyProperty.Register(
            nameof(FocusedBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBorderBrush
    {
        get => (Brush?)GetValue(FocusedBorderBrushProperty);
        set => SetValue(FocusedBorderBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="DropDownOpenForeground"/> dependency property.</summary>
    public static readonly DependencyProperty DropDownOpenForegroundProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenForeground
    {
        get => (Brush?)GetValue(DropDownOpenForegroundProperty);
        set => SetValue(DropDownOpenForegroundProperty, value);
    }

    /// <summary>Identifies the <see cref="DropDownOpenBackground"/> dependency property.</summary>
    public static readonly DependencyProperty DropDownOpenBackgroundProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenBackground
    {
        get => (Brush?)GetValue(DropDownOpenBackgroundProperty);
        set => SetValue(DropDownOpenBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="DropDownOpenBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty DropDownOpenBorderBrushProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenBorderBrush
    {
        get => (Brush?)GetValue(DropDownOpenBorderBrushProperty);
        set => SetValue(DropDownOpenBorderBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="DisabledForeground"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledForegroundProperty =
        DependencyProperty.Register(
            nameof(DisabledForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledForeground
    {
        get => (Brush?)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    /// <summary>Identifies the <see cref="DisabledBackground"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledBackgroundProperty =
        DependencyProperty.Register(
            nameof(DisabledBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBackground
    {
        get => (Brush?)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="DisabledBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledBorderBrushProperty =
        DependencyProperty.Register(
            nameof(DisabledBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBorderBrush
    {
        get => (Brush?)GetValue(DisabledBorderBrushProperty);
        set => SetValue(DisabledBorderBrushProperty, value);
    }

    private static readonly DependencyPropertyKey VisualForegroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="VisualForeground"/> dependency property.</summary>
    public static readonly DependencyProperty VisualForegroundProperty =
        VisualForegroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the foreground selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal foreground value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualForeground => (Brush?)GetValue(VisualForegroundProperty);

    private static readonly DependencyPropertyKey VisualBackgroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="VisualBackground"/> dependency property.</summary>
    public static readonly DependencyProperty VisualBackgroundProperty =
        VisualBackgroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the background selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal background value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBackground => (Brush?)GetValue(VisualBackgroundProperty);

    private static readonly DependencyPropertyKey VisualBorderBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="VisualBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty VisualBorderBrushProperty =
        VisualBorderBrushPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the border brush selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal border brush value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBorderBrush => (Brush?)GetValue(VisualBorderBrushProperty);

    #endregion

    static ComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(typeof(ComboBox))
        );

        // ----------------------------------------------------------------------------------------
        // Standard Control brushes remain the normal-state inputs. Metadata callbacks keep the
        // template-facing outputs synchronized when styles, bindings, or dynamic resources change.
        // ----------------------------------------------------------------------------------------

        BackgroundProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                BackgroundProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        BorderBrushProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                BorderBrushProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                ForegroundProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        // -------------------------------------------------------------------------------------------
        // These inherited properties have no overridable CLR transition hooks. Overriding metadata
        // appends our callback to WPF's existing callbacks and preserves the framework's coercion and
        // two-way-binding behavior while making runtime mode changes update the visual outputs.
        // -------------------------------------------------------------------------------------------

        IsEditableProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );

        IsReadOnlyProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );

        IsDropDownOpenProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );
    }

    /// <summary>Initializes a new instance of the <see cref="ComboBox"/> class.</summary>
    public ComboBox()
    {
        UpdateVisualBrushes();
        IsEnabledChanged += OnComboBoxIsEnabledChanged;
    }

    private static void OnStandardBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ComboBox)d).UpdateVisualBrushes();
    }

    private static void OnStateBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ComboBox)d).UpdateVisualBrushes();
    }

    private static void OnInteractionStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((ComboBox)d).UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        // WPF may close the popup in its base implementation when focus leaves. Updating after the
        // base call ensures both the focus state and any resulting IsDropDownOpen change are settled.
        base.OnIsKeyboardFocusWithinChanged(e);
        UpdateVisualBrushes();
    }

    private void OnComboBoxIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Resolves all template-facing brushes in one pass. Disabled always wins. Editable controls
    /// behave like text input and therefore prefer keyboard focus over hover; their open popup is
    /// not a pressed visual state. Non-editable controls behave like toggle buttons, so an open
    /// popup wins over hover. Read-only does not form an independent visual state because WPF only
    /// applies it to the embedded editor; theme styles can specialize its focused brushes.
    /// </summary>
    private void UpdateVisualBrushes()
    {
        Brush? visualForeground  = Foreground;
        Brush? visualBackground  = Background;
        Brush? visualBorderBrush = BorderBrush;

        if (!IsEnabled)
        {
            visualForeground  = DisabledForeground ?? visualForeground;
            visualBackground  = DisabledBackground ?? visualBackground;
            visualBorderBrush = DisabledBorderBrush ?? visualBorderBrush;
        }
        else if (IsEditable && IsKeyboardFocusWithin)
        {
            visualForeground  = FocusedForeground ?? visualForeground;
            visualBackground  = FocusedBackground ?? visualBackground;
            visualBorderBrush = FocusedBorderBrush ?? visualBorderBrush;
        }
        else if (!IsEditable && IsDropDownOpen)
        {
            visualForeground  = DropDownOpenForeground ?? visualForeground;
            visualBackground  = DropDownOpenBackground ?? visualBackground;
            visualBorderBrush = DropDownOpenBorderBrush ?? visualBorderBrush;
        }
        else if (IsMouseOver)
        {
            visualForeground  = MouseOverForeground ?? visualForeground;
            visualBackground  = MouseOverBackground ?? visualBackground;
            visualBorderBrush = MouseOverBorderBrush ?? visualBorderBrush;
        }

        SetValue(VisualForegroundPropertyKey, visualForeground);
        SetValue(VisualBackgroundPropertyKey, visualBackground);
        SetValue(VisualBorderBrushPropertyKey, visualBorderBrush);
    }
}
