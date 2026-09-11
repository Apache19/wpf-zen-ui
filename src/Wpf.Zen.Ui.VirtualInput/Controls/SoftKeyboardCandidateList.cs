// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Displays one input-method candidate page without exposing a general-purpose Items collection.
/// The host supplies immutable candidate data and the current backend index; this control owns
/// generated containers, current-item presentation and scrolling, but never commits input itself.
/// </summary>
/// <remarks>
/// The optional PART_ItemsHost ContentPresenter receives an internally owned, non-virtualizing
/// items surface. Templates should put it inside a ScrollViewer. Omitting the part removes candidate
/// presentation safely. Candidate item styles may change appearance but cannot change item identity.
/// Direct child items and caller-supplied containers are not supported.
/// </remarks>
[TemplatePart(Name = ItemsHostPartName, Type = typeof(ContentPresenter))]
public sealed class SoftKeyboardCandidateList : Control
{
    private const string ItemsHostPartName = "PART_ItemsHost";

    private readonly SoftKeyboardCandidateItemsHost _itemsHost;

    private ContentPresenter?    _presenter;
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
        _itemsHost = new SoftKeyboardCandidateItemsHost(this);

        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemClick));

        Loaded   += (_, _) => QueueReveal();
        Unloaded += (_, _) => CancelReveal();

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                QueueReveal();
            }
            else
            {
                CancelReveal();
            }
        };
    }

    /// <summary>
    /// Identifies the <see cref="Candidates"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidatesProperty
        = DependencyProperty.Register(
            nameof(Candidates),
            typeof(IEnumerable<SoftKeyboardCandidate>),
            typeof(SoftKeyboardCandidateList),
            new PropertyMetadata(null, OnCandidatesChanged)
        );

    /// <summary>
    /// Gets or sets the candidate page supplied by the input method; the default is null.
    /// </summary>
    /// <remarks>
    /// Observable sources update through WPF collection views. Plain enumerables update when replaced.
    /// The source must contain non-null candidates with unique backend indices. This property is not
    /// inherited or animated; changes regenerate affected containers and request current-item visibility.
    /// </remarks>
    public IEnumerable<SoftKeyboardCandidate>? Candidates
    {
        get => (IEnumerable<SoftKeyboardCandidate>?)GetValue(CandidatesProperty);
        set => SetValue(CandidatesProperty, value);
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
    /// The value is not inherited or coerced and does not itself force synchronous layout.
    /// </remarks>
    public uint? SelectedCandidateIndex
    {
        get => (uint?)GetValue(SelectedCandidateIndexProperty);
        set => SetValue(SelectedCandidateIndexProperty, value);
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
    /// Occurs when a current generated container is invoked; its host validates and commits the request.
    /// </summary>
    public event EventHandler<SoftKeyboardCandidateInvokedEventArgs> CandidateInvoked
    {
        add => AddHandler(CandidateInvokedEvent, value);
        remove => RemoveHandler(CandidateInvokedEvent, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _presenter = GetTemplateChild(ItemsHostPartName) as ContentPresenter;

        if (_presenter is not null)
        {
            _presenter.SetCurrentValue(ContentPresenter.ContentProperty, _itemsHost);
        }

        QueueReveal();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CancelReveal();

        if (_presenter is not null)
        {
            // The same internal items surface is reused, but cannot belong to two visual parents.
            _presenter.SetCurrentValue(ContentPresenter.ContentProperty, null);
            _presenter = null;
        }

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private static void OnCandidatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var list = (SoftKeyboardCandidateList)d;
        list._itemsHost.SetCurrentValue(ItemsControl.ItemsSourceProperty, e.NewValue);
        list.QueueReveal();
    }

    private static void OnSelectedCandidateIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var list = (SoftKeyboardCandidateList)d;
        for (var index = 0; index < list._itemsHost.Items.Count; index++)
        {
            if (list._itemsHost.ItemContainerGenerator.ContainerFromIndex(index) is SoftKeyboardCandidateItem container)
            {
                list.UpdateSelection(container);
            }
        }

        list.QueueReveal();
    }

    internal void UpdateSelection(SoftKeyboardCandidateItem container)
    {
        container.SetIsSelected(
            SelectedCandidateIndex.HasValue
            && container.Candidate?.Index == SelectedCandidateIndex
        );
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is SoftKeyboardCandidateItem container
            && ItemsControl.ItemsControlFromItemContainer(container) == _itemsHost
            && _itemsHost.ItemContainerGenerator.ItemFromContainer(container) is SoftKeyboardCandidate candidate)
        {
            // The generator, not mutable presentation Content, identifies the invoked data item.
            e.Handled = true;
            RaiseEvent(new SoftKeyboardCandidateInvokedEventArgs(this, candidate));
        }
    }

    // Loaded priority coalesces binding updates and also handles render-only selection changes.
    // A pending request survives an unarranged container; LayoutUpdated is used only for that retry.
    internal void QueueReveal()
    {
        CancelReveal();

        if (SelectedCandidateIndex.HasValue
            && IsLoaded && IsVisible && _presenter is not null)
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

        if (!_revealPending || !IsLoaded || !IsVisible || _presenter is null)
        {
            CancelReveal();
            return;
        }

        for (var index = 0; index < _itemsHost.Items.Count; index++)
        {
            if (_itemsHost.Items[index] is not SoftKeyboardCandidate candidate ||
                candidate.Index != SelectedCandidateIndex)
            {
                continue;
            }

            if (_itemsHost.ItemContainerGenerator.ContainerFromIndex(index) is SoftKeyboardCandidateItem container
                && container.IsVisible && container.IsArrangeValid)
            {
                // Detach before scrolling because BringIntoView schedules additional layout.
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

        // No candidate on this page matches. A later collection update starts a fresh request.
        CancelReveal();
    }
}
