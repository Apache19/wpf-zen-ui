// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library */

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents one content segment and the following separator in a <see cref="BreadcrumbBar"/>.
/// </summary>
/// <remarks>
/// <para>
/// The item is a lightweight <see cref="ContentControl"/>, not a button. The default template
/// assigns input, focus and command handling only to <c>PART_Button</c>, while it renders the
/// separator beside that part. Consequently, the separator remains visual structure and cannot
/// activate the breadcrumb segment.
/// </para>
/// <para>
/// Custom templates may omit <c>PART_Button</c>; the item will still render, but it will be
/// non-interactive. When the part is present, its command properties should be template-bound to
/// <see cref="Command"/>, <see cref="CommandParameter"/> and <see cref="CommandTarget"/> so WPF's
/// native button command pipeline continues to provide <c>CanExecute</c> state and routed-command
/// targeting.
/// </para>
/// </remarks>
[DefaultEvent(nameof(Click))]
[TemplatePart(Name = ButtonPartName, Type = typeof(ButtonBase))]
public class BreadcrumbBarItem : ContentControl, ICommandSource
{
    private const string ButtonPartName = "PART_Button";

    private ButtonBase? _button;

    #region Routed Event: Click

    /// <summary>Identifies the <see cref="Click"/> routed event.</summary>
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        nameof(Click),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(BreadcrumbBarItem)
    );

    /// <summary>
    /// Occurs when the button part representing this breadcrumb segment is activated.
    /// Clicking the adjacent separator does not raise this event.
    /// </summary>
    [Category("Behavior")]
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    #endregion

    #region DP: Command

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(
        typeof(BreadcrumbBarItem),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the command executed by the template button after <see cref="Click"/> is raised.
    /// </summary>
    /// <remarks>
    /// The default template forwards this value to its button part. This deliberately leaves
    /// command execution and <see cref="ICommand.CanExecute"/> observation to WPF's
    /// <see cref="ButtonBase"/> implementation instead of duplicating its internal behavior.
    /// </remarks>
    [Bindable(true)]
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    #endregion

    #region DP: CommandParameter

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        ButtonBase.CommandParameterProperty.AddOwner(
            typeof(BreadcrumbBarItem),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
    [Bindable(true)]
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #endregion

    #region DP: CommandTarget

    /// <summary>Identifies the <see cref="CommandTarget"/> dependency property.</summary>
    public static readonly DependencyProperty CommandTargetProperty =
        ButtonBase.CommandTargetProperty.AddOwner(
            typeof(BreadcrumbBarItem),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the element on which a routed <see cref="Command"/> is executed.
    /// A <see langword="null"/> value lets WPF use the template button as the command source.
    /// </summary>
    [Bindable(true)]
    [Category("Action")]
    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    #endregion

    #region Read-only DP: IsLast

    private static readonly DependencyPropertyKey IsLastPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsLast),
            typeof(bool),
            typeof(BreadcrumbBarItem),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsLast"/> dependency property.</summary>
    public static readonly DependencyProperty IsLastProperty = IsLastPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the current item is the last one.
    /// </summary>
    /// <remarks>
    /// The owning <see cref="BreadcrumbBar"/> maintains this value as containers are generated or
    /// cleared. It is read-only because callers cannot change an item's structural position.
    /// </remarks>
    public bool IsLast => (bool)GetValue(IsLastProperty);

    /// <summary>Updates the position state owned by the parent item generator.</summary>
    internal void SetIsLast(bool value) => SetValue(IsLastPropertyKey, value);

    #endregion

    static BreadcrumbBarItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(BreadcrumbBarItem),
            new FrameworkPropertyMetadata(typeof(BreadcrumbBarItem))
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// Event hookup occurs only after the template is applied because templates may replace the
    /// button implementation. A missing or differently typed part is tolerated and degrades to a
    /// presentation-only item rather than failing control construction.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachButton();
        base.OnApplyTemplate();

        _button = GetTemplateChild(ButtonPartName) as ButtonBase;
        _button?.Click += OnButtonClick;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // A template can be replaced without an immediate layout pass. Detach eagerly so a retained
        // old visual cannot continue translating clicks while the new template awaits application.
        DetachButton();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>Raises the control-owned <see cref="Click"/> event.</summary>
    protected virtual void OnClick()
    {
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        // Do not leak the private ButtonBase event through the template boundary. BreadcrumbBar and
        // application code observe only the stable BreadcrumbBarItem event whose source is this
        // container. ButtonBase executes its bound command after this handler returns, preserving
        // WPF's native Click-before-Command ordering.
        e.Handled = true;
        OnClick();
    }

    private void DetachButton()
    {
        if (_button is null)
        {
            return;
        }

        _button.Click -= OnButtonClick;
        _button = null;
    }
}
