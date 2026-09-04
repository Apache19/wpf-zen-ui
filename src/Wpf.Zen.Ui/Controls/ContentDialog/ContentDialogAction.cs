// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes one semantic footer action independently of its generated button.
/// Use ContentTemplate for data-driven content and ButtonStyle for the complete WPF button appearance.
/// </summary>
/// <remarks>
/// This Freezable receives an inheritance context when assigned to a dialog dependency property, so
/// ordinary DataContext bindings work without placing the definition in the visual tree.
/// Use a separate definition per dialog when bindings depend on that dialog's DataContext.
/// Properties do not inherit or perform layout themselves; bindings update the generated button.
/// The dialog establishes ButtonStyle bindings on named button parts; templates bind the remaining
/// presentation properties and provide the routed commands for their semantic actions.
/// Commands run synchronously. For asynchronous validation use ClosesDialog=false and call Hide
/// only after the operation succeeds; the definition does not guess when an ICommand has finished.
/// </remarks>
[ContentProperty(nameof(Content))]
[StyleTypedProperty(Property = nameof(ButtonStyle), StyleTargetType = typeof(Button))]
public sealed class ContentDialogAction : Freezable
{
    #region DP: Content

    /// <summary>Identifies the <see cref="Content"/> dependency property.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets button content. Default null; non-string data can be displayed with ContentTemplate.</summary>
    public object? Content
    {
        get => (object?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    #endregion

    #region DP: ContentTemplate

    /// <summary>Identifies the <see cref="ContentTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty ContentTemplateProperty = DependencyProperty.Register(
        nameof(ContentTemplate),
        typeof(DataTemplate),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the template used to render Content. Default null uses WPF content presentation.</summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    #endregion

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets optional icon content. Default null omits the icon.</summary>
    public object? Icon
    {
        get => (object?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: Appearance

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(ControlAppearance),
        typeof(ContentDialogAction),
        new PropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>Gets or sets the semantic appearance. Default Normal; choose Accent explicitly for a recommended action.</summary>
    public ControlAppearance Appearance
    {
        get => (ControlAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    #endregion

    #region DP: ButtonStyle

    /// <summary>Identifies the <see cref="ButtonStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register(
        nameof(ButtonStyle),
        typeof(Style),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets a style targeting the library Button. Default null uses the button part's template-supplied style. Use style setters for padding, tooltips, automation metadata or a complete ControlTemplate; action bindings own content and command properties.</summary>
    /// <remarks>The owning dialog establishes the style binding on its named button part, so custom dialog templates do not need to repeat it. Replacing the action or changing this property updates that part automatically.</remarks>
    public Style? ButtonStyle
    {
        get => (Style?)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    #endregion

    #region DP: IsEnabled

    /// <summary>Identifies the <see cref="IsEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register(
        nameof(IsEnabled),
        typeof(bool),
        typeof(ContentDialogAction),
        new PropertyMetadata(true)
    );

    /// <summary>Gets or sets a value indicating whether the action is enabled. Default true; false disables the button without hiding it. Command.CanExecute must also succeed.</summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    #endregion

    #region DP: Command

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the optional command. Default null. CanExecute participates in availability; execution precedes automatic closure.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    #endregion

    #region DP: CommandParameter

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(ContentDialogAction),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the command argument. Default null.</summary>
    public object? CommandParameter
    {
        get => (object?)GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #endregion

    #region DP: ClosesDialog

    /// <summary>Identifies the <see cref="ClosesDialog"/> dependency property.</summary>
    public static readonly DependencyProperty ClosesDialogProperty = DependencyProperty.Register(
        nameof(ClosesDialog),
        typeof(bool),
        typeof(ContentDialogAction),
        new PropertyMetadata(true)
    );

    /// <summary>Gets or sets a value indicating whether successful synchronous execution closes the dialog with this action's result. Default true. Set false for validation or asynchronous work.</summary>
    public bool ClosesDialog
    {
        get => (bool)GetValue(ClosesDialogProperty);
        set => SetValue(ClosesDialogProperty, value);
    }

    #endregion

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore() => new ContentDialogAction();
}
