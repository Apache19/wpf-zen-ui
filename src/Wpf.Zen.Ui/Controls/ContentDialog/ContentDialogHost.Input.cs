// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Owns the modal input boundary, covered-content isolation, focus confinement, and focus lifecycle of a content dialog session.
/// </summary>
public partial class ContentDialogHost
{
    private readonly List<UIElement> _disabledElements = [];
    private readonly List<TitleBar> _isolatedTitleBars = [];
    private WeakReference<IInputElement>? _focusBeforeDialog;
    private UIElement? _inputRoot;

    /// <summary>
    /// Establishes the modal boundary before the dialog enters the visual tree.
    /// </summary>
    /// <remarks>
    /// Keyboard focus must be captured before covered content is disabled, because WPF may move focus as a side
    /// effect of changing <see cref="UIElement.IsEnabled"/>. The weak reference follows WPF's own menu focus
    /// restoration pattern: removing application content while a dialog is open must not keep that content alive.
    /// </remarks>
    private void StartModalInteraction(ContentDialog dialog, UIElement contentRoot)
    {
        RememberKeyboardFocus();
        _contentRoot = contentRoot;
        ApplyContentIsolation();
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
    }

    /// <summary>
    /// Releases the modal boundary and restores focus before the public presentation task completes.
    /// </summary>
    private void StopModalInteraction(ContentDialog dialog)
    {
        RestoreContentIsolation();
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
        RestoreKeyboardFocus(dialog);
    }

    private void RememberKeyboardFocus()
    {
        _focusBeforeDialog = Keyboard.FocusedElement is { } focused
            ? new WeakReference<IInputElement>(focused)
            : null;
    }

    private void RestoreKeyboardFocus(ContentDialog dialog)
    {
        var rememberedFocus = _focusBeforeDialog;
        _focusBeforeDialog = null;

        // Removing the dialog normally leaves keyboard focus on its detached subtree (or clears it). If focus has
        // deliberately moved to an independent region, preserve that newer user choice instead of stealing it.
        var currentFocus = Keyboard.FocusedElement;
        if (currentFocus is not null && !Contains(dialog, currentFocus as DependencyObject))
            return;

        if (rememberedFocus?.TryGetTarget(out var target) == true)
            _ = TryRestoreKeyboardFocus(target);
    }

    private static bool TryRestoreKeyboardFocus(IInputElement target) => target switch
    {
        UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element => element.Focus(),
        ContentElement { IsEnabled: true, Focusable: true } element => element.Focus(),
        UIElement3D { IsVisible: true, IsEnabled: true, Focusable: true } element => element.Focus(),
        _ => false,
    };

    /// <summary>
    /// Updates the template-owned root whose application content must be isolated during presentation.
    /// </summary>
    /// <remarks>
    /// A control template can be replaced while a dialog is open. Restoring the old tree before isolating the new
    /// tree prevents detached content from retaining host-owned enabled values after it leaves the template.
    /// </remarks>
    private void SetContentRoot(UIElement? contentRoot)
    {
        if (ReferenceEquals(_contentRoot, contentRoot))
            return;

        RestoreContentIsolation();
        _contentRoot = contentRoot;
        if (_session is not null)
            ApplyContentIsolation();
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
    private void ApplyContentIsolation()
    {
        if (_contentRoot is not { } contentRoot || _disabledElements.Count != 0 || _isolatedTitleBars.Count != 0)
            return;

        var titleBars = FindTitleBars(contentRoot);
        if (titleBars.Count == 0)
        {
            DisableElement(contentRoot);
            return;
        }

        var titleBarPaths = new HashSet<DependencyObject>();
        foreach (var titleBar in titleBars)
        {
            DependencyObject? current = titleBar;
            while (current is not null)
            {
                titleBarPaths.Add(current);
                if (ReferenceEquals(current, contentRoot))
                    break;
                current = VisualTreeHelper.GetParent(current);
            }
        }

        IsolateBranches(contentRoot, titleBarPaths);
    }

    private void IsolateBranches(DependencyObject current, HashSet<DependencyObject> titleBarPaths)
    {
        if (current is TitleBar titleBar)
        {
            if (titleBar.IsUserContentEnabled)
            {
                titleBar.IsUserContentEnabled = false;
                _isolatedTitleBars.Add(titleBar);
            }

            return;
        }

        if (!titleBarPaths.Contains(current) && current is UIElement element)
        {
            DisableElement(element);
            return;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(current);
        for (int index = 0; index < childCount; index++)
            IsolateBranches(VisualTreeHelper.GetChild(current, index), titleBarPaths);
    }

    private void DisableElement(UIElement element)
    {
        if (!element.IsEnabled)
            return;

        // SetCurrentValue preserves bindings and dynamic-resource expressions. The host owns the effective value
        // for the presentation lifetime and restores it before focus can return to application content.
        element.SetCurrentValue(UIElement.IsEnabledProperty, false);
        _disabledElements.Add(element);
    }

    private void RestoreContentIsolation()
    {
        foreach (var titleBar in _isolatedTitleBars)
            titleBar.IsUserContentEnabled = true;
        _isolatedTitleBars.Clear();

        foreach (var element in _disabledElements)
            element.SetCurrentValue(UIElement.IsEnabledProperty, true);
        _disabledElements.Clear();
    }

    private static List<TitleBar> FindTitleBars(DependencyObject root)
    {
        var result = new List<TitleBar>();
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
                pending.Push(VisualTreeHelper.GetChild(current, index));
        }

        return result;
    }

    private void OnAccessKey(object sender, AccessKeyPressedEventArgs e)
    {
        // Button.UpdateIsDefaulted and AccessKeyManager query the same bubbling event. A focus scope alone does not
        // isolate access keys. Preserve inner scopes, such as a menu, when one has already claimed the event.
        if (e.Scope is null)
            e.Scope = _session?.Dialog;
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_session?.Dialog is not { } dialog)
            return;
        // Editors and popup children get the first opportunity to consume keys. At the boundary use WPF's access-key
        // machinery, preserving IsDefault, disabled targets and AcceptsReturn. Handling here also stops ancestor
        // InputBindings, including bindings backed by a plain ICommand.
        if (e.Key == Key.Escape)
        {
            if (dialog.IsDismissible)
            {
                if (AccessKeyManager.IsKeyRegistered(dialog, "\x001B"))
                    AccessKeyManager.ProcessKey(dialog, "\x001B", false);
                else
                    dialog.Dismiss();
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
                    ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next,
                Key.Left => FocusNavigationDirection.Left,
                Key.Right => FocusNavigationDirection.Right,
                Key.Up => FocusNavigationDirection.Up,
                Key.Down => FocusNavigationDirection.Down,
                _ => null,
            };
            if (direction is { } navigation)
            {
                var request = new TraversalRequest(navigation);
                if (Keyboard.FocusedElement is UIElement element)
                    element.MoveFocus(request);
                else if (Keyboard.FocusedElement is FrameworkContentElement content)
                    content.MoveFocus(request);
            }
        }

        e.Handled = true;
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        var text = string.IsNullOrEmpty(e.Text) ? e.SystemText : e.Text;
        if (_session?.Dialog is { } dialog && !string.IsNullOrEmpty(text)
            && System.Globalization.StringInfo.ParseCombiningCharacters(text).Length == 1)
            AccessKeyManager.ProcessKey(dialog, text, false);
        e.Handled = true;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (_session?.Dialog is not { } dialog || _inputRoot is not { } inputRoot)
            return;

        // PreviewGotKeyboardFocus is the cancellable half of WPF's focus transaction. Observing it through
        // InputManager is intentional: a Popup or ContextMenu has a separate presentation source, so an event
        // handler on the dialog's visual tree cannot see every focus transfer. Contains follows placement ownership,
        // allowing surfaces opened by the dialog while rejecting every other target in this presentation root.
        // A target in another Window has a different root and remains available when the application switches away.
        if (e.StagingItem.Input is KeyboardFocusChangedEventArgs focus
            && focus.RoutedEvent == Keyboard.PreviewGotKeyboardFocusEvent
            && Contains(inputRoot, focus.NewFocus as DependencyObject)
            && !Contains(dialog, focus.NewFocus as DependencyObject))
            focus.Handled = true;
    }

    private bool IsCoveredTarget(object source) => _session?.Dialog is { } dialog
        && source is DependencyObject target && !Contains(dialog, target)
        && (Contains(this, target) || Contains(dialog, Keyboard.FocusedElement as DependencyObject));

    // Button.OnClick uses this internal WPF command when IsCancel=true and Command=null. Identify its public command
    // metadata without reflecting Window internals. Unknown commands still stop at the dialog boundary, so a WPF
    // rename cannot accidentally route the command to Window and close it.
    private static bool IsWindowCancel(ICommand command) =>
        command is RoutedCommand { OwnerType: var owner, Name: "DialogCancel" } && owner == typeof(Window);

    private void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (_session?.Dialog is { } dialog && Contains(dialog, e.OriginalSource as DependencyObject)
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
        if (_session?.Dialog is { } dialog && Contains(dialog, e.OriginalSource as DependencyObject)
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

    private void OnExecutedBoundary(object sender, ExecutedRoutedEventArgs e) => e.Handled = true;

    /// <summary>
    /// Traverses both ownership and presentation edges. Popup and ContextMenu use PlacementTarget because their
    /// separate HWND breaks visual ancestry; visited nodes protect against malformed ownership cycles.
    /// </summary>
    internal static bool Contains(DependencyObject ancestor, DependencyObject? element)
    {
        if (element is null)
            return false;
        var pending = new Stack<DependencyObject>();
        var visited = new HashSet<DependencyObject>();
        pending.Push(element);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
                continue;
            if (ReferenceEquals(current, ancestor))
                return true;
            void Push(DependencyObject? parent)
            {
                if (parent is not null)
                    pending.Push(parent);
            }

            if (current is Visual or Visual3D)
                Push(VisualTreeHelper.GetParent(current));
            Push(LogicalTreeHelper.GetParent(current));
            if (current is FrameworkElement frameworkElement)
                Push(frameworkElement.TemplatedParent);
            if (current is FrameworkContentElement frameworkContentElement)
                Push(frameworkContentElement.Parent);
            if (current is Popup popup)
                Push(popup.PlacementTarget);
            if (current is ContextMenu menu)
                Push(menu.PlacementTarget);
        }

        return false;
    }
}
