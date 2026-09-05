// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Owns the modal input boundary, covered-content isolation, focus confinement, and focus lifecycle of a content dialog session.
/// </summary>
public partial class ContentDialogHost
{
    private          WeakReference<IInputElement>? _focusBeforeDialog;
    private          UIElement?      _inputRoot;
    private          int             _focusSessionVersion;

    /// <summary>
    /// Establishes the modal boundary before the dialog enters the visual tree.
    /// </summary>
    /// <remarks>
    /// Keyboard focus must be captured before covered content is disabled, because WPF may move focus as a side
    /// effect of changing <see cref="UIElement.IsEnabled"/>. The weak reference follows WPF's own menu focus
    /// restoration pattern: removing application content while a dialog is open must not keep that content alive.
    /// </remarks>
    private void StartModalInteraction(Session session, UIElement contentRoot)
    {
        var dialog = session.Dialog;
        RememberKeyboardFocus();

        _contentRoot = contentRoot;

        _inputRoot = PresentationSource.FromVisual(this)?.RootVisual as UIElement;

        AccessKeyManager.AddAccessKeyPressedHandler(dialog, OnAccessKey);

        dialog.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        dialog.AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(OnTextInput));

        CommandManager.AddCanExecuteHandler(dialog, OnCanExecuteBoundary);
        CommandManager.AddExecutedHandler(dialog, OnExecutedBoundary);

        // The presentation root sees explicit CommandTarget routes into covered content, even when those routes
        // bypass the dialog. Regions outside this host remain independent and continue to accept input.
        if (_inputRoot is not null)
        {
            CommandManager.AddPreviewCanExecuteHandler(_inputRoot, OnPreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(_inputRoot, OnPreviewExecuted);
        }

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        LayoutUpdated += OnIsolationLayoutUpdated;

        // Install all subscriptions before coercion can call application code. Reentrant cleanup
        // can now remove everything, and there is no initialization work after this callback boundary.
        ApplyContentIsolation(session);
    }

    /// <summary>
    /// Releases the modal boundary and attempts focus restoration before the public presentation task completes.
    /// Temporarily unavailable targets receive one deferred retry after task continuations.
    /// </summary>
    private void StopModalInteraction(Session session)
    {
        var dialog = session.Dialog;
        // Unhook global input before restoring properties: application IsEnabledChanged handlers
        // may throw or pump Dispatcher, but must not retain this host through InputManager.
        LayoutUpdated -= OnIsolationLayoutUpdated;
        session.IsolationRefreshOperation?.Abort();
        session.IsolationRefreshOperation = null;
        AccessKeyManager.RemoveAccessKeyPressedHandler(dialog, OnAccessKey);

        dialog.RemoveHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        dialog.RemoveHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(OnTextInput));

        CommandManager.RemoveCanExecuteHandler(dialog, OnCanExecuteBoundary);
        CommandManager.RemoveExecutedHandler(dialog, OnExecutedBoundary);

        if (_inputRoot is not null)
        {
            CommandManager.RemovePreviewCanExecuteHandler(_inputRoot, OnPreviewCanExecute);
            CommandManager.RemovePreviewExecutedHandler(_inputRoot, OnPreviewExecuted);
        }

        _inputRoot = null;

        InputManager.Current.PreProcessInput -= OnPreProcessInput;

        try
        {
            RestoreContentIsolation(session);
        }
        finally
        {
            RestoreKeyboardFocus(dialog);
        }
    }

    private void RememberKeyboardFocus()
    {
        _focusSessionVersion++;
        _focusBeforeDialog = Keyboard.FocusedElement is { } focused
            ? new WeakReference<IInputElement>(focused)
            : null;
    }

    private void RestoreKeyboardFocus(ContentDialog dialog)
    {
        var rememberedFocus = _focusBeforeDialog;
        _focusBeforeDialog = null;

        var root = PresentationSource.FromVisual(this)?.RootVisual;
        if (!CanRestoreFocus())
        {
            return;
        }

        if (rememberedFocus?.TryGetTarget(out var target) == true)
        {
            if (TryRestoreKeyboardFocus(target) || Dispatcher.HasShutdownStarted)
            {
                return;
            }

            // An async command commonly disables its invoking button until ShowAsync and its caller finish.
            // Retry once after normal-priority task continuations and binding updates have re-enabled it.
            // A new session invalidates this callback even if that session has already closed by then.
            int version = _focusSessionVersion;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (_session is null && version == _focusSessionVersion && CanRestoreFocus()
                    && rememberedFocus.TryGetTarget(out var pendingTarget))
                {
                    _ = TryRestoreKeyboardFocus(pendingTarget);
                }
            }));
        }

        bool CanRestoreFocus()
        {
            // KeyboardDevice.ReevaluateFocusCallback falls back to the presentation root when a focused
            // subtree is detached. That fallback is not a user focus choice. Preserve all other outside targets.
            var current = Keyboard.FocusedElement;
            return current is null || ReferenceEquals(current, root)
                   || Contains(dialog, current as DependencyObject);
        }
    }

    private static bool TryRestoreKeyboardFocus(IInputElement target) => target switch
    {
        UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element   => element.Focus(),
        ContentElement { IsEnabled: true, Focusable: true } element               => element.Focus(),
        UIElement3D { IsVisible: true, IsEnabled: true, Focusable: true } element => element.Focus(),
        _                                                                         => false,
    };

    /// <summary>
    /// Updates the template-owned root whose application content must be isolated during presentation.
    /// </summary>
    /// <remarks>
    /// A control template can be replaced while a dialog is open. Reconciliation constrains its new
    /// branches and releases outgoing leases so detached content does not retain modal coercion.
    /// </remarks>
    private void SetContentRoot(UIElement? contentRoot)
    {
        if (ReferenceEquals(_contentRoot, contentRoot))
        {
            return;
        }

        _contentRoot = contentRoot;
        if (_session is { } session && IsActive(session))
        {
            RefreshContentIsolation(session);
        }
    }

    /// <summary>
    /// Disables covered application branches while keeping the title-bar window shell operational.
    /// </summary>
    /// <remarks>
    /// WPF force-inherits <see cref="UIElement.IsEnabled"/> from the nearest input parent. Disabling the content
    /// presenter therefore cannot be undone by setting a nested title bar back to true. Instead, the host preserves
    /// each visual path from the presenter to a <see cref="TitleBar"/> and disables sibling branches at their highest
    /// possible element. User-provided title-bar content remains ordinary client UI and is isolated separately.
    /// </remarks>
    private void ApplyContentIsolation(Session session)
    {
        if (!IsActive(session))
        {
            return;
        }

        if (session.IsUpdatingIsolation)
        {
            session.NeedsIsolationRefresh = true;
            return;
        }

        session.NeedsIsolationRefresh = false;
        session.IsUpdatingIsolation = true;
        try
        {
            // Snapshot the tree before invoking any property callbacks. LayoutUpdated is WPF's
            // public post-layout notification for template realization and arbitrary descendant
            // changes. Reconcile only changed branches; stable layouts never recoerce properties.
            var targets = new HashSet<DependencyObject>();
            if (_contentRoot is { } contentRoot)
            {
                var titleBarPaths = new HashSet<DependencyObject>();
                foreach (var titleBar in FindTitleBars(contentRoot))
                {
                    DependencyObject? current = titleBar;
                    while (current is not null)
                    {
                        titleBarPaths.Add(current);
                        if (ReferenceEquals(current, contentRoot))
                        {
                            break;
                        }

                        current = VisualTreeHelper.GetParent(current);
                    }
                }

                CollectIsolationTargets(contentRoot, titleBarPaths, targets);
            }

            // Constrain incoming branches first, so switching between a whole-root constraint and
            // title-bar paths does not briefly enable client content during property callbacks.
            foreach (var target in targets)
            {
                if (!IsActive(session))
                {
                    return;
                }

                if (!session.InputIsolation.ContainsKey(target))
                {
                    var property  = target is TitleBar ? TitleBar.IsUserContentEnabledProperty : IsEnabledProperty;
                    var isolation = new ContentDialogInputIsolation(target, property);

                    session.InputIsolation.Add(target, isolation);
                    isolation.Activate();
                }
            }

            foreach (var target in session.InputIsolation.Keys.Where(target => !targets.Contains(target)).ToArray())
            {
                if (!IsActive(session))
                {
                    return;
                }

                var isolation = session.InputIsolation[target];
                session.InputIsolation.Remove(target);
                isolation.Release();
            }
        }
        finally
        {
            session.IsUpdatingIsolation = false;

            if (IsActive(session) && session.NeedsIsolationRefresh && session.IsolationRefreshOperation is null)
            {
                // An application callback can rebuild and lay out the tree while this snapshot is
                // being applied. Defer one fresh snapshot until it unwinds instead of dropping that
                // change or recursively coercing the same branch from its IsEnabledChanged handler.
                session.IsolationRefreshOperation = Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() =>
                        {
                            session.IsolationRefreshOperation = null;
                            RefreshContentIsolation(session);
                        }
                    )
                );
            }
        }
    }

    private static void CollectIsolationTargets(
        DependencyObject current,
        HashSet<DependencyObject> titleBarPaths,
        HashSet<DependencyObject> targets)
    {
        if (current is TitleBar)
        {
            targets.Add(current);
            return;
        }

        if (!titleBarPaths.Contains(current) && current is UIElement element)
        {
            targets.Add(element);
            return;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(current);
        for (int index = 0; index < childCount; index++)
        {
            CollectIsolationTargets(VisualTreeHelper.GetChild(current, index), titleBarPaths, targets);
        }
    }

    private void OnIsolationLayoutUpdated(object? sender, EventArgs e)
    {
        if (_session is not { } session || !IsActive(session))
        {
            return;
        }

        RefreshContentIsolation(session);
    }

    /// <summary>Contains failures from template/layout callbacks outside the presentation worker.</summary>
    private void RefreshContentIsolation(Session session)
    {
        try
        {
            ApplyContentIsolation(session);
        }
        catch (Exception exception)
        {
            // Layout callbacks are outside PresentAsync's try/catch. Report failure through the
            // session task while synchronously releasing every already established constraint.
            CompleteSession(session, session.OutcomeResult, exception);
        }
    }

    private static void RestoreContentIsolation(Session session)
    {
        var isolation = session.InputIsolation.Values.ToArray();

        session.InputIsolation.Clear();

        Exception? failure = null;

        foreach (var lease in isolation)
        {
            try
            {
                lease.Release();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static List<TitleBar> FindTitleBars(DependencyObject root)
    {
        var result  = new List<TitleBar>();
        var pending = new Stack<DependencyObject>();

        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is TitleBar titleBar)
            {
                result.Add(titleBar);
                // A nested title bar is part of this title bar's user content and must not create another enabled
                // shell path. IsUserContentEnabled disables that entire application-owned subtree instead.
                continue;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(current);
            for (int index = 0; index < childCount; index++)
            {
                pending.Push(VisualTreeHelper.GetChild(current, index));
            }
        }

        return result;
    }

    private void OnAccessKey(object sender, AccessKeyPressedEventArgs e)
    {
        // Button.UpdateIsDefaulted and AccessKeyManager query the same bubbling event. A focus scope alone does not
        // isolate access keys. Preserve inner scopes, such as a menu, when one has already claimed the event.
        if (e.Scope is null)
        {
            e.Scope = _session?.Dialog;
        }

        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_session?.Dialog is not { } dialog)
        {
            return;
        }

        // Editors and popup children get the first opportunity to consume keys. At the boundary use WPF's access-key
        // machinery, preserving IsDefault, disabled targets and AcceptsReturn. Handling here also stops ancestor
        // InputBindings, including bindings backed by a plain ICommand.
        if (e.Key == Key.Escape)
        {
            if (dialog.IsDismissible)
            {
                if (AccessKeyManager.IsKeyRegistered(dialog, "\x001B"))
                {
                    AccessKeyManager.ProcessKey(dialog, "\x001B", false);
                }
                else
                {
                    dialog.Dismiss();
                }
            }
        }
        else if (e.Key == Key.Enter)
        {
            AccessKeyManager.ProcessKey(dialog, "\r", false);
        }
        else
        {
            // KeyboardNavigation normally runs after routing, but the boundary must mark input handled before
            // ancestor InputBindings see it. MoveFocus enters WPF's navigation engine, including nested groups.
            FocusNavigationDirection? direction = e.Key switch
            {
                Key.Tab => (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next,

                Key.Left  => FocusNavigationDirection.Left,
                Key.Right => FocusNavigationDirection.Right,
                Key.Up    => FocusNavigationDirection.Up,
                Key.Down  => FocusNavigationDirection.Down,
                _         => null,
            };

            if (direction is { } navigation)
            {
                var request = new TraversalRequest(navigation);
                if (Keyboard.FocusedElement is UIElement element)
                {
                    element.MoveFocus(request);
                }
                else if (Keyboard.FocusedElement is FrameworkContentElement content)
                {
                    content.MoveFocus(request);
                }
            }
        }

        e.Handled = true;
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        var text = string.IsNullOrEmpty(e.Text) ? e.SystemText : e.Text;

        if (_session?.Dialog is { } dialog
            && !string.IsNullOrEmpty(text)
            && StringInfo.ParseCombiningCharacters(text).Length == 1)
        {
            AccessKeyManager.ProcessKey(dialog, text, false);
        }

        e.Handled = true;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (_session?.Dialog is not { } dialog || _inputRoot is not { } inputRoot)
        {
            return;
        }

        // PreviewGotKeyboardFocus is the cancellable half of WPF's focus transaction. Observing it through
        // InputManager is intentional: a Popup or ContextMenu has a separate presentation source, so an event
        // handler on the dialog's visual tree cannot see every focus transfer. Contains follows placement ownership,
        // allowing surfaces opened by the dialog while rejecting every other target in this presentation root.
        // A target in another Window has a different root and remains available when the application switches away.
        if (e.StagingItem.Input is KeyboardFocusChangedEventArgs focus
            && focus.RoutedEvent == Keyboard.PreviewGotKeyboardFocusEvent
            && Contains(inputRoot, focus.NewFocus as DependencyObject)
            && !Contains(dialog, focus.NewFocus as DependencyObject))
        {
            focus.Handled = true;
        }
    }

    private bool IsCoveredTarget(object source)
    {
        return _session?.Dialog is { } dialog
               && source is DependencyObject target && !Contains(dialog, target)
               && (Contains(this, target)
                   || Contains(dialog, Keyboard.FocusedElement as DependencyObject));
    }

    // Button.OnClick uses this internal WPF command when IsCancel=true and Command=null. Identify its public command
    // metadata without reflecting Window internals. Unknown commands still stop at the dialog boundary, so a WPF
    // rename cannot accidentally route the command to Window and close it.
    private static bool IsWindowCancel(ICommand command)
    {
        return command is RoutedCommand { OwnerType: var owner, Name: "DialogCancel" }
               && owner == typeof(Window);
    }

    private void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (_session?.Dialog is { } dialog
            && Contains(dialog, e.OriginalSource as DependencyObject)
            && IsWindowCancel(e.Command))
        {
            // Consume even when non-dismissible; otherwise Window's class binding can handle it.
            e.CanExecute = true;
            e.Handled = true;
        }
        else if (IsCoveredTarget(e.OriginalSource))
        {
            e.CanExecute = false;
            e.Handled = true;
        }
    }

    private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_session?.Dialog is { } dialog
            && Contains(dialog, e.OriginalSource as DependencyObject)
            && IsWindowCancel(e.Command))
        {
            dialog.Dismiss();
            e.Handled = true;
        }
        else if (IsCoveredTarget(e.OriginalSource))
        {
            e.Handled = true;
        }
    }

    private void OnCanExecuteBoundary(object sender, CanExecuteRoutedEventArgs e)
    {
        // CommandBindings on the dialog, body, or footer run before this bubbling boundary.
        e.CanExecute = false;
        e.Handled = true;
    }

    private void OnExecutedBoundary(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// Traverses both ownership and presentation edges.
    /// Popup and ContextMenu use PlacementTarget because their separate HWND breaks visual ancestry;
    /// visited nodes protect against malformed ownership cycles.
    /// </summary>
    internal static bool Contains(DependencyObject ancestor, DependencyObject? element)
    {
        if (element is null)
        {
            return false;
        }

        var pending = new Stack<DependencyObject>();
        var visited = new HashSet<DependencyObject>();

        pending.Push(element);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            if (current is Visual or Visual3D)
            {
                Push(VisualTreeHelper.GetParent(current));
            }

            Push(LogicalTreeHelper.GetParent(current));

            if (current is FrameworkElement frameworkElement)
            {
                Push(frameworkElement.TemplatedParent);
            }

            if (current is FrameworkContentElement frameworkContentElement)
            {
                Push(frameworkContentElement.Parent);
            }

            if (current is Popup popup)
            {
                Push(popup.PlacementTarget);
            }

            if (current is ContextMenu menu)
            {
                Push(menu.PlacementTarget);
            }

            continue;

            void Push(DependencyObject? parent)
            {
                if (parent is not null)
                {
                    pending.Push(parent);
                }
            }
        }

        return false;
    }
}
