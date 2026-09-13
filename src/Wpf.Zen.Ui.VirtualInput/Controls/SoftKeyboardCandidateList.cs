// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Presents an input-method candidate page through the standard WPF items-control model.
/// </summary>
/// <remarks>
/// <para>
/// Applications can replace <see cref="ItemsControl.ItemsPanel"/>,
/// <see cref="ItemsControl.ItemTemplate"/> and <see cref="ItemsControl.ItemContainerStyle"/> without
/// replacing candidate behavior. Generated containers do not take keyboard focus from the active editor.
/// </para>
/// <para>
/// Invoking a candidate raises <see cref="CandidateInvoked"/> first. Unless a handler marks the event as
/// handled, <see cref="Command"/> is executed with the candidate as its parameter. The default command is
/// <see cref="SoftKeyboardCommands.CommitCandidate"/>.
/// </para>
/// </remarks>
public class SoftKeyboardCandidateList : ItemsControl
{
    private DispatcherOperation? _revealOperation;

    private bool _revealPending;
    private bool _waitingForLayout;

    static SoftKeyboardCandidateList()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SoftKeyboardCandidateList),
            new FrameworkPropertyMetadata(typeof(SoftKeyboardCandidateList))
        );

        FocusableProperty.OverrideMetadata(
            typeof(SoftKeyboardCandidateList),
            new FrameworkPropertyMetadata(false)
        );

        IsTabStopProperty.OverrideMetadata(
            typeof(SoftKeyboardCandidateList),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardCandidateList"/> class.
    /// </summary>
    public SoftKeyboardCandidateList()
    {
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemClick));

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>
    /// Identifies the <see cref="SelectedCandidateIndex"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SelectedCandidateIndexProperty
        = DependencyProperty.Register(
            nameof(SelectedCandidateIndex),
            typeof(uint?),
            typeof(SoftKeyboardCandidateList),
            new PropertyMetadata(null, OnSelectedCandidateIndexChanged)
        );

    /// <summary>
    /// Gets or sets the input method's current backend index, or null when none is current.
    /// </summary>
    /// <remarks>
    /// Defaults to null and is not a page-relative position. An index outside the current page selects
    /// no container. Changes update appearance and scrolling only; they never invoke a candidate.
    /// </remarks>
    public uint? SelectedCandidateIndex
    {
        get => (uint?)GetValue(SelectedCandidateIndexProperty);
        set => SetValue(SelectedCandidateIndexProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Command"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CommandProperty
        = DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(SoftKeyboardCandidateList),
            new PropertyMetadata(SoftKeyboardCommands.CommitCandidate)
        );

    /// <summary>
    /// Gets or sets the command invoked for a candidate whose routed event was not handled.
    /// </summary>
    /// <remarks>
    /// The candidate is supplied as the command parameter. The default command routes to the containing
    /// <see cref="SoftKeyboard"/>. Set this property to null to use only <see cref="CandidateInvoked"/>.
    /// </remarks>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CommandTarget"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CommandTargetProperty
        = DependencyProperty.Register(
            nameof(CommandTarget),
            typeof(IInputElement),
            typeof(SoftKeyboardCandidateList),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the explicit target for a routed candidate command.
    /// </summary>
    /// <remarks>
    /// When null, routed commands start at this list and follow the normal WPF command route.
    /// Non-routed commands ignore this property.
    /// </remarks>
    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CandidateInvoked"/> routed event.
    /// </summary>
    public static readonly RoutedEvent CandidateInvokedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(CandidateInvoked),
            RoutingStrategy.Bubble,
            typeof(EventHandler<SoftKeyboardCandidateInvokedEventArgs>),
            typeof(SoftKeyboardCandidateList)
        );

    /// <summary>
    /// Occurs when a current generated container is invoked.
    /// </summary>
    /// <remarks>
    /// Mark the event as handled to suppress automatic execution of <see cref="Command"/>.
    /// </remarks>
    public event EventHandler<SoftKeyboardCandidateInvokedEventArgs> CandidateInvoked
    {
        add => AddHandler(CandidateInvokedEvent, value);
        remove => RemoveHandler(CandidateInvokedEvent, value);
    }

    /// <summary>
    /// Creates the container used to present one candidate.
    /// </summary>
    /// <returns>A non-focusable candidate container.</returns>
    protected virtual SoftKeyboardCandidateItem CreateCandidateContainer()
    {
        return new SoftKeyboardCandidateItem();
    }

    /// <summary>
    /// Called after a generated candidate container has been prepared.
    /// </summary>
    /// <param name="container">The prepared container.</param>
    /// <param name="candidate">The immutable candidate represented by the container.</param>
    protected virtual void OnCandidateContainerPrepared(
        SoftKeyboardCandidateItem container,
        SoftKeyboardCandidate candidate)
    {
    }

    /// <summary>
    /// Called before a generated candidate container is cleared for reuse.
    /// </summary>
    /// <param name="container">The container being cleared.</param>
    /// <param name="candidate">The immutable candidate previously represented by the container.</param>
    protected virtual void OnCandidateContainerClearing(
        SoftKeyboardCandidateItem container,
        SoftKeyboardCandidate candidate)
    {
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return false;
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride()
    {
        return CreateCandidateContainer();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is not SoftKeyboardCandidateItem container
            || item is not SoftKeyboardCandidate candidate)
        {
            throw new InvalidOperationException(
                $"{nameof(SoftKeyboardCandidateList)} items must be {nameof(SoftKeyboardCandidate)} instances."
            );
        }

        base.PrepareContainerForItemOverride(element, item);

        container.SetCandidate(candidate);

        UpdateSelection(container);
        OnCandidateContainerPrepared(container, candidate);
        QueueReveal();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is SoftKeyboardCandidateItem { Candidate: { } candidate } container)
        {
            OnCandidateContainerClearing(container, candidate);

            container.SetCandidate(null);
            container.SetIsSelected(false);

            if (container.IsMouseCaptured)
            {
                container.ReleaseMouseCapture();
            }
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        QueueReveal();
    }

    private static void OnSelectedCandidateIndexChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var list = (SoftKeyboardCandidateList)d;
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is SoftKeyboardCandidateItem container)
            {
                list.UpdateSelection(container);
            }
        }

        list.QueueReveal();
    }

    private void UpdateSelection(SoftKeyboardCandidateItem container)
    {
        container.SetIsSelected(
            SelectedCandidateIndex.HasValue
            && container.Candidate?.Index == SelectedCandidateIndex
        );
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not SoftKeyboardCandidateItem container
            || ItemsControlFromItemContainer(container) != this
            || ItemContainerGenerator.ItemFromContainer(container) is not SoftKeyboardCandidate candidate)
        {
            return;
        }

        e.Handled = true;

        var invokedArgs = new SoftKeyboardCandidateInvokedEventArgs(this, candidate);
        RaiseEvent(invokedArgs);

        if (!invokedArgs.Handled)
        {
            ExecuteCommand(candidate);
        }
    }

    private void ExecuteCommand(SoftKeyboardCandidate candidate)
    {
        if (Command is RoutedCommand routedCommand)
        {
            IInputElement target = CommandTarget ?? this;
            if (routedCommand.CanExecute(candidate, target))
            {
                routedCommand.Execute(candidate, target);
            }

            return;
        }

        if (Command?.CanExecute(candidate) == true)
        {
            Command.Execute(candidate);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QueueReveal();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelReveal();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            QueueReveal();
        }
        else
        {
            CancelReveal();
        }
    }

    private void QueueReveal()
    {
        CancelReveal();

        if (SelectedCandidateIndex.HasValue && IsLoaded && IsVisible)
        {
            _revealPending = true;

            _revealOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(RevealCurrentCandidate)
            );
        }
    }

    private void CancelReveal()
    {
        _revealOperation?.Abort();
        _revealOperation = null;
        _revealPending   = false;

        StopWaitingForLayout();
    }

    private void StopWaitingForLayout()
    {
        if (_waitingForLayout)
        {
            LayoutUpdated -= OnPendingLayoutUpdated;
            _waitingForLayout = false;
        }
    }

    private void OnPendingLayoutUpdated(object? sender, EventArgs e)
    {
        RevealCurrentCandidate();
    }

    private void RevealCurrentCandidate()
    {
        _revealOperation = null;

        if (!_revealPending || !IsLoaded || !IsVisible)
        {
            CancelReveal();
            return;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index] is not SoftKeyboardCandidate candidate
                || candidate.Index != SelectedCandidateIndex)
            {
                continue;
            }

            if (ItemContainerGenerator.ContainerFromIndex(index)
                is SoftKeyboardCandidateItem { IsVisible: true, IsArrangeValid: true } container)
            {
                CancelReveal();
                container.BringIntoView();
            }
            else if (!_waitingForLayout)
            {
                _waitingForLayout = true;
                LayoutUpdated += OnPendingLayoutUpdated;
            }

            return;
        }

        CancelReveal();
    }
}
