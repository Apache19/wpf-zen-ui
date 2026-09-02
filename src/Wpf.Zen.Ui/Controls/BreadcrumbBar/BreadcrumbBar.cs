// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// The <see cref="BreadcrumbBar"/> control provides the direct path of pages or folders to the current location.
/// </summary>
/// <remarks>
/// <para>
/// The control is a composite <see cref="ICommandSource"/>: each non-final breadcrumb segment is
/// an invocation surface, and the corresponding source item becomes <see cref="CommandParameter"/>
/// for the duration of that invocation.
/// </para>
/// <para>
/// <see cref="ICommand.CanExecute(object)"/> is evaluated independently for every realized
/// segment. A command can therefore disable one path target without disabling the complete bar.
/// The final segment represents the current location and is never governed by the bar command.
/// </para>
/// <para>
/// The complete breadcrumb path contributes one stop to the surrounding Tab sequence. Tab enters
/// the first or most recently focused segment and the following Tab leaves the bar; arrow keys move
/// focus spatially among the segment buttons without escaping the bar.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:BreadcrumbBar x:Name="BreadcrumbBar" /&gt;
/// </code>
/// </example>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(BreadcrumbBarItem))]
public class BreadcrumbBar : ItemsControl, ICommandSource
{
    private int _userActionDepth;

    #region DP: Command

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(
        typeof(BreadcrumbBar),
        new FrameworkPropertyMetadata(null, OnCommandChanged)
    );

    /// <summary>
    /// Gets or sets the command executed after a non-final breadcrumb item is activated.
    /// </summary>
    /// <remarks>
    /// The activated source item is supplied as <see cref="CommandParameter"/>. Command execution
    /// follows <see cref="ItemClicked"/>, matching the event-before-command ordering of
    /// <see cref="ButtonBase"/>.
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

    #region Read-only DP: CommandParameter

    private static readonly DependencyPropertyKey CommandParameterPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CommandParameter),
            typeof(object),
            typeof(BreadcrumbBar),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        CommandParameterPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the source item for the user action currently being dispatched.
    /// </summary>
    /// <remarks>
    /// The value is <see langword="null"/> while no breadcrumb action is in progress. It is
    /// read-only because the item generator, rather than the consumer, determines which source
    /// item corresponds to an activated container.
    /// </remarks>
    [Bindable(true)]
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public object? CommandParameter => GetValue(CommandParameterProperty);

    #endregion

    #region DP: CommandTarget

    /// <summary>Identifies the <see cref="CommandTarget"/> dependency property.</summary>
    public static readonly DependencyProperty CommandTargetProperty =
        ButtonBase.CommandTargetProperty.AddOwner(
            typeof(BreadcrumbBar),
            new FrameworkPropertyMetadata(null, OnCommandTargetChanged)
        );

    /// <summary>
    /// Gets or sets the element at which a routed <see cref="Command"/> starts routing.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="null"/>, the activated <see cref="BreadcrumbBarItem"/>
    /// is used. This keeps routed-command lookup anchored to the visual source of the action
    /// instead of the element that happened to own keyboard focus.
    /// </remarks>
    [Bindable(true)]
    [Category("Action")]
    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    #endregion

    /// <summary>Identifies the <see cref="ItemClicked"/> routed event.</summary>
    public static readonly RoutedEvent ItemClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(ItemClicked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs>),
        typeof(BreadcrumbBar)
    );

    /// <summary>
    /// Occurs when an item is clicked in the <see cref="BreadcrumbBar"/>.
    /// </summary>
    public event TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs> ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    static BreadcrumbBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(BreadcrumbBar),
            new FrameworkPropertyMetadata(typeof(BreadcrumbBar))
        );

        // BreadcrumbBar is a composite control: its template buttons remain independently
        // focusable for activation, but the complete path must occupy only one place in the
        // surrounding Tab order. These metadata defaults mirror WPF's ListBox focus contract and
        // remain replaceable through the standard attached properties when a host needs a
        // specialized navigation policy.
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(
            typeof(BreadcrumbBar),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Once)
        );
        KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(
            typeof(BreadcrumbBar),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BreadcrumbBar"/> class.
    /// </summary>
    public BreadcrumbBar()
    {
        // BreadcrumbBarItem translates only its bounded template button into this event. Listening
        // for the container-owned event therefore excludes separator clicks while still identifying
        // the exact generated container without an ancestor binding or template command.
        AddHandler(BreadcrumbBarItem.ClickEvent, new RoutedEventHandler(OnItemContainerClick));
        ItemContainerGenerator.ItemsChanged += ItemContainerGeneratorOnItemsChanged;
        ItemContainerGenerator.StatusChanged += ItemContainerGeneratorOnStatusChanged;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="KeyboardNavigation.DirectionalNavigationProperty"/> defines the boundary for
    /// WPF's focus search, but <see cref="ItemsControl"/> does not initiate that search when an
    /// arrow key is pressed. The focused template button bubbles unhandled key input to the bar,
    /// which translates it into spatial focus traversal. WPF consequently handles left-to-right
    /// and right-to-left layouts from the arranged positions rather than requiring logical index
    /// reversal here.
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (
            TryGetFocusNavigationDirection(e.Key, out FocusNavigationDirection direction)
            && Keyboard.FocusedElement is UIElement focusedElement
            && IsKeyboardFocusWithin
        )
        {
            _ = focusedElement.MoveFocus(new TraversalRequest(direction));

            // Directional input belongs to the composite even at an edge. Contained keeps focus on
            // the current segment there; marking the event handled prevents an outer scrolling or
            // command surface from interpreting the same arrow after focus traversal has finished.
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Maps the four spatial arrow keys to WPF focus-navigation directions.</summary>
    private static bool TryGetFocusNavigationDirection(
        Key key,
        out FocusNavigationDirection direction)
    {
        direction = key switch
        {
            Key.Left => FocusNavigationDirection.Left,
            Key.Up => FocusNavigationDirection.Up,
            Key.Right => FocusNavigationDirection.Right,
            Key.Down => FocusNavigationDirection.Down,
            _ => default,
        };

        return key is Key.Left or Key.Up or Key.Right or Key.Down;
    }

    /// <summary>
    /// Raises <see cref="ItemClicked"/> and executes <see cref="Command"/> for an activated item.
    /// </summary>
    /// <param name="item">The original item supplied through the items collection or source.</param>
    /// <param name="container">The realized container that received the input.</param>
    /// <param name="index">The current index of <paramref name="item"/>.</param>
    /// <remarks>
    /// The source item is deliberately kept separate from the container so derived controls can
    /// customize invocation without leaking presentation objects into view-model commands.
    /// </remarks>
    protected virtual void OnItemClicked(object item, BreadcrumbBarItem container, int index)
    {
        var previousParameter = CommandParameter;
        _userActionDepth++;
        SetValue(CommandParameterPropertyKey, item);

        try
        {
            var args = new BreadcrumbBarItemClickedEventArgs(ItemClickedEvent, this, item, container, index);
            RaiseEvent(args);

            // Keep the source-item snapshot stable even when an ItemClicked handler synchronously
            // changes ItemsSource or otherwise causes the original container to be recycled.
            ExecuteCommand(item, container);
        }
        finally
        {
            _userActionDepth--;
            SetValue(
                CommandParameterPropertyKey,
                _userActionDepth == 0 ? null : previousParameter
            );

            if (_userActionDepth == 0)
            {
                UpdateContainerCommandStates();
            }
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is BreadcrumbBarItem;
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new BreadcrumbBarItem();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        UpdateContainerPositions();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is BreadcrumbBarItem container)
        {
            container.SetIsLast(false);
            // Containers may be recycled by a custom items panel. Remove the owner-specific
            // command constraint before the container is returned to the generator.
            container.SetOwningCommandCanExecute(true);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    private void ItemContainerGeneratorOnStatusChanged(object? sender, EventArgs e)
    {
        if (ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        UpdateContainerPositions();
    }

    private void ItemContainerGeneratorOnItemsChanged(object sender, ItemsChangedEventArgs e)
    {
        UpdateContainerPositions();
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BreadcrumbBar)d;

        if (e.OldValue is ICommand oldCommand)
        {
            CanExecuteChangedEventManager.RemoveHandler(oldCommand, control.OnCanExecuteChanged);
        }

        if (e.NewValue is ICommand newCommand)
        {
            // Weak subscription prevents a long-lived view-model command from retaining a bar
            // that has been removed from the visual tree.
            CanExecuteChangedEventManager.AddHandler(newCommand, control.OnCanExecuteChanged);
        }

        control.UpdateContainerCommandStates();
    }

    private static void OnCommandTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((BreadcrumbBar)d).UpdateContainerCommandStates();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateContainerCommandStates();
    }

    private void OnItemContainerClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not BreadcrumbBarItem container || container.IsLast)
        {
            return;
        }

        int index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            return;
        }

        object item = ItemContainerGenerator.ItemFromContainer(container);
        OnItemClicked(item, container, index);
    }

    /// <summary>
    /// Recomputes positional state for every realized container after generator changes.
    /// </summary>
    /// <remarks>
    /// Breadcrumb bars are short, non-virtualized paths. Updating all realized containers avoids
    /// stale IsLast values after Move, Replace, Reset, or source replacement without coupling the
    /// algorithm to the shape of a particular collection notification.
    /// </remarks>
    private void UpdateContainerPositions()
    {
        int lastIndex = Items.Count - 1;
        for (int index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is BreadcrumbBarItem container)
            {
                container.SetIsLast(index == lastIndex);
                UpdateContainerCommandState(container, Items[index], index == lastIndex);
            }
        }
    }

    /// <summary>
    /// Re-evaluates the bar command against every realized source item.
    /// </summary>
    /// <remarks>
    /// Breadcrumb paths are short and non-virtualized, so a complete pass is both predictable and
    /// less error-prone than retaining per-container command subscriptions. Unrealized containers
    /// receive the same state when the generator prepares them.
    /// </remarks>
    private void UpdateContainerCommandStates()
    {
        int lastIndex = Items.Count - 1;
        for (int index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is BreadcrumbBarItem container)
            {
                UpdateContainerCommandState(container, Items[index], index == lastIndex);
            }
        }
    }

    private void UpdateContainerCommandState(BreadcrumbBarItem container, object item, bool isLast)
    {
        // The final segment is current-location presentation rather than a navigation request.
        // Its enabled state must remain independent from a command that the bar never invokes.
        container.SetOwningCommandCanExecute(isLast || CanExecuteCommand(item, container));
    }

    private bool CanExecuteCommand(object item, BreadcrumbBarItem container)
    {
        return Command switch
        {
            null                  => true,
            RoutedCommand routed => routed.CanExecute(item, CommandTarget ?? container),
            _                     => Command.CanExecute(item)
        };
    }

    private void ExecuteCommand(object item, BreadcrumbBarItem container)
    {
        switch (Command)
        {
            case RoutedCommand routed when routed.CanExecute(item, CommandTarget ?? container):
                routed.Execute(item, CommandTarget ?? container);
                break;

            case { } command when command.CanExecute(item):
                command.Execute(item);
                break;
        }
    }
}
