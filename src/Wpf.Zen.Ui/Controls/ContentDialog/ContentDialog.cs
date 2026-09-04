// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.AutomationPeers;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents modal content within an explicitly assigned ContentDialogHost.
/// Three optional semantic actions occupy the footer; header dismissal reports no choice.
/// </summary>
/// <remarks>
/// Standard FrameworkElement sizing applies to the dialog surface. The host owns the overlay,
/// transitions and presentation scope. Optional button parts receive styles and assist initial focus; custom templates
/// invoke InvokeActionCommand with a ContentDialogButton parameter and CloseCommand for dismissal.
/// PART_ContentPresenter identifies the realized content for initial focus, including DataTemplates.
/// Missing parts degrade to focusing the dialog. No template part is required for ShowAsync/Hide.
/// </remarks>
[TemplatePart(Name = "PART_PrimaryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_SecondaryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_TertiaryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ContentPresenter", Type = typeof(ContentPresenter))]
public partial class ContentDialog : ContentControl
{
    private readonly HashSet<ICommand> _observedCommands = [];
    private ContentDialogHost? _dialogHost;
    private TaskCompletionSource<ContentDialogResult>? _completion;
    private bool _opening;
    private bool _executingAction;

    /// <summary>Invokes a footer action identified by a ContentDialogButton command parameter.</summary>
    public static readonly RoutedCommand InvokeActionCommand = new(nameof(InvokeActionCommand), typeof(ContentDialog));

    /// <summary>Dismisses the dialog with None when IsDismissible is true.</summary>
    public static readonly RoutedCommand CloseCommand = new(nameof(CloseCommand), typeof(ContentDialog));

    static ContentDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentDialog), new FrameworkPropertyMetadata(typeof(ContentDialog)));
    }

    /// <summary>Initializes a new instance of the <see cref="ContentDialog"/> class. Assign DialogHost before showing it.</summary>
    public ContentDialog()
    {
        CommandBindings.Add(new CommandBinding(InvokeActionCommand, OnExecuteAction, OnCanExecuteAction));
        CommandBindings.Add(new CommandBinding(
            CloseCommand,
            (_, e) =>
            {
                Hide();
                e.Handled = true;
            },
            (_, e) =>
            {
                e.CanExecute = CanInteract && IsDismissible;
                e.Handled = true;
            }
        ));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Initializes a new instance of the <see cref="ContentDialog"/> class for the specified presentation scope.</summary>
    public ContentDialog(ContentDialogHost dialogHost)
        : this()
    {
        DialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
    }

    /// <summary>Gets or sets the host. It may be changed between presentations, but not while showing.</summary>
    public ContentDialogHost? DialogHost
    {
        get => _dialogHost;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(_dialogHost, value))
                return;

            if (IsOpen)
                throw new InvalidOperationException("Cannot change DialogHost during a presentation.");

            _dialogHost = value;
        }
    }

    private bool CanInteract => _completion is { Task.IsCompleted: false } && !_executingAction
        && ReferenceEquals(_dialogHost?.Content, this);

    /// <summary>Connects styles to optional button parts whenever WPF realizes a template.</summary>
    /// <remarks>
    /// As with WPF Calendar's generated buttons, the control establishes the style bindings.
    /// Template authors need only preserve part names; action replacement and nested ButtonStyle
    /// changes are observed by WPF binding without extra event subscriptions.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        BindButtonStyle("PART_PrimaryButton", "PrimaryAction.ButtonStyle");
        BindButtonStyle("PART_SecondaryButton", "SecondaryAction.ButtonStyle");
        BindButtonStyle("PART_TertiaryButton", "TertiaryAction.ButtonStyle");
        BindButtonStyle("PART_CloseButton", nameof(CloseButtonStyle));
    }

    private void BindButtonStyle(string partName, string path)
    {
        if (GetTemplateChild(partName) is not ButtonBase button)
            return;

        // Retain the template's default style when no override is supplied (in particular the
        // close glyph and accessible name). Missing actions also use this fallback. Capture it
        // before installing our local binding, which deliberately owns the part's Style property.
        var fallback = button.Style;
        button.SetBinding(StyleProperty, new Binding(path)
        {
            Source = this,
            Mode = BindingMode.OneWay,
            TargetNullValue = fallback,
            FallbackValue = fallback,
        });
    }

    /// <summary>
    /// Shows this dialog until a choice, dismissal, cancellation or host removal completes it.
    /// The task completes after exit animation and cleanup. Cancellation returns None.
    /// </summary>
    /// <remarks>
    /// Call on the host Dispatcher after it has loaded. A busy host and concurrent reuse of an
    /// instance are rejected, so unrelated prompts cannot silently discard a user's pending choice.
    /// Closed is raised while this presentation still reserves the instance; await the returned
    /// task before reusing it. The cancellation callback never touches WPF objects on a worker thread.
    /// </remarks>
    public async Task<ContentDialogResult> ShowAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();

        var host = _dialogHost ?? throw new InvalidOperationException("DialogHost was not set.");

        host.VerifyAccess();

        if (IsOpen)
            throw new InvalidOperationException("This ContentDialog is already being shown.");

        if (!host.IsLoaded)
            throw new InvalidOperationException("ContentDialogHost must be loaded before showing a dialog.");

        if (!host.IsVisible)
            throw new InvalidOperationException("ContentDialogHost must be visible before showing a dialog.");

        if (host.Content is not null)
            throw new InvalidOperationException("ContentDialogHost is busy. Await the current dialog before showing another.");

        if (Parent is not null || VisualTreeHelper.GetParent(this) is not null)
            throw new InvalidOperationException("A hosted ContentDialog must not already have a parent.");

        if (cancellationToken.IsCancellationRequested)
            return ContentDialogResult.None;

        var completion = new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        _completion = completion;
        _opening = false;

        SetValue(IsOpenPropertyKey, true);
        SetValue(IsClosingPropertyKey, false);

        // Task completion is thread-safe. All presentation changes resume on the UI Dispatcher.
        using var registration = cancellationToken.Register(() => completion.TrySetResult(ContentDialogResult.None));

        var result = ContentDialogResult.None;
        try
        {
            RefreshCommandSubscriptions();

            host.SetCurrentValue(ContentControl.ContentProperty, this);

            // A custom host template can omit its content presenter altogether. Wait until WPF
            // has had a layout/load opportunity, then release a presentation which cannot be seen.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (ReferenceEquals(_completion, completion) && (!IsLoaded || !IsVisible))
                    NotifyHostRemoved();
            }));

            result = await completion.Task;

            SetValue(IsClosingPropertyKey, true);

            CommandManager.InvalidateRequerySuggested();

            await host.CloseDialogAsync(this);

            return result;
        }
        finally
        {
            // An externally replaced dialog must never clear its successor. Cleanup also runs
            // when an application event handler throws during attachment or animation startup.
            try
            {
                if (ReferenceEquals(host.Content, this))
                    host.SetCurrentValue(ContentControl.ContentProperty, null);
            }
            finally
            {
                DetachCommands();
                try
                {
                    OnClosed(result);
                }
                finally
                {
                    _completion = null;

                    SetValue(IsOpenPropertyKey, false);
                    SetValue(IsClosingPropertyKey, false);
                }
            }
        }
    }

    /// <summary>
    /// Requests non-cancellable closure. The first result wins; repeated calls or calls while
    /// not showing do nothing. Await ShowAsync to observe completion of the exit transition.
    /// </summary>
    public virtual void Hide(ContentDialogResult result = ContentDialogResult.None)
    {
        VerifyAccess();

        if (result is not (ContentDialogResult.None or ContentDialogResult.Primary or ContentDialogResult.Secondary or ContentDialogResult.Tertiary))
            throw new ArgumentOutOfRangeException(nameof(result));

        if (_completion?.TrySetResult(result) == true)
        {
            SetValue(IsClosingPropertyKey, true);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Raises the final, non-cancellable lifecycle notification after presentation cleanup.</summary>
    protected virtual void OnClosed(ContentDialogResult result)
    {
        RaiseEvent(new ContentDialogClosedEventArgs(ClosedEvent, this) { Result = result });
    }

    /// <summary>
    /// Validates and executes a semantic action. ButtonClicked.Cancel rejects this invocation.
    /// Commands finish before automatic closure; ClosesDialog=false permits asynchronous validation.
    /// </summary>
    protected virtual void OnButtonClick(ContentDialogButton button)
    {
        var action = GetAction(button);

        if (!CanInteract || action is null || !CanExecute(action))
            return;

        _executingAction = true;

        var operation = _completion!;

        try
        {
            var args = new ContentDialogButtonClickEventArgs(ButtonClickedEvent, this) { Button = button };

            RaiseEvent(args);

            // Application handlers can replace actions, dismiss the dialog, or pump the Dispatcher.
            // Only the original, still-active presentation is permitted to execute the captured action.
            if (args.Cancel || operation != _completion || operation.Task.IsCompleted
                || !ReferenceEquals(_dialogHost?.Content, this) || !ReferenceEquals(action, GetAction(button)) || !CanExecute(action))
            {
                return;
            }

            if (action.Command is RoutedCommand routed)
                routed.Execute(action.CommandParameter, this);
            else
                action.Command?.Execute(action.CommandParameter);

            if (action.ClosesDialog && ReferenceEquals(operation, _completion))
            {
                Hide(button switch
                {
                    ContentDialogButton.Primary   => ContentDialogResult.Primary,
                    ContentDialogButton.Secondary => ContentDialogResult.Secondary,
                    ContentDialogButton.Tertiary  => ContentDialogResult.Tertiary,
                    _ => ContentDialogResult.None,
                });
            }
        }
        finally
        {
            _executingAction = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    internal void NotifyHostRemoved()
    {
        _completion?.TrySetResult(ContentDialogResult.None);
    }

    private ContentDialogAction? GetAction(ContentDialogButton button) => button switch
    {
        ContentDialogButton.Primary   => PrimaryAction,
        ContentDialogButton.Secondary => SecondaryAction,
        ContentDialogButton.Tertiary  => TertiaryAction,
        _ => null,
    };

    private bool CanExecute(ContentDialogAction action)
    {
        if (!action.IsEnabled)
            return false;

        return action.Command switch
        {
            RoutedCommand command when command == InvokeActionCommand || command == CloseCommand => false,
            RoutedCommand command => command.CanExecute(action.CommandParameter, this),
            { } command => command.CanExecute(action.CommandParameter),
            _ => true,
        };
    }

    private void OnCanExecuteAction(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = CanInteract && e.Parameter is ContentDialogButton button
            && GetAction(button) is { } action && CanExecute(action);

        e.Handled = true;
    }

    private void OnExecuteAction(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ContentDialogButton button)
            OnButtonClick(button);

        e.Handled = true;
    }

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var dialog = (ContentDialog)d;

        // Freezable subproperty changes also reach this callback. Refresh weak command subscriptions
        // so replacing a command or changing its CanExecute immediately updates the visible button.
        if (dialog.IsOpen)
            dialog.RefreshCommandSubscriptions();

        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnCommandStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshCommandSubscriptions()
    {
        DetachCommands();

        foreach (ContentDialogAction? action in new[] { PrimaryAction, SecondaryAction, TertiaryAction })
        {
            if (action?.Command is { } command && _observedCommands.Add(command))
            {
                CanExecuteChangedEventManager.AddHandler(command, OnActionCanExecuteChanged);
            }
        }
    }

    private void DetachCommands()
    {
        foreach (ICommand command in _observedCommands)
        {
            CanExecuteChangedEventManager.RemoveHandler(command, OnActionCanExecuteChanged);
        }

        _observedCommands.Clear();
    }

    private void OnActionCanExecuteChanged(object? sender, EventArgs e)
    {
        // ICommand implementations may signal from a worker thread. Marshal only the invalidation.
        if (!Dispatcher.HasShutdownStarted)
            _ = Dispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_opening || !CanInteract)
        {
            return;
        }

        _opening = true;

        var operation = _completion;

        if (!IsFocusInsideDialog())
        {
            SetInitialFocus();
        }

        await _dialogHost!.OpenDialogAsync(this);

        if (ReferenceEquals(operation, _completion) && CanInteract)
        {
            RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var operation = _completion;

        // Replacing a host template can detach the dialog even while Content still refers to it.
        // A deferred check lets WPF finish a transient unload/load pair; a genuinely missing
        // visual must complete the session rather than leaving an invisible modal operation alive.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (ReferenceEquals(operation, _completion) && !IsLoaded)
                NotifyHostRemoved();
        }));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ContentDialogAutomationPeer(this);
    }
}
