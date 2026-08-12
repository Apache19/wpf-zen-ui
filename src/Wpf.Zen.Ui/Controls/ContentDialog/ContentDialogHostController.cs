// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Wpf.Zen.Ui.Extensions.Wpf;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable IDE0008 // Use explicit type instead of 'var'

/// <summary>
/// Controller that manages the runtime behavior of a single <see cref="ContentDialogHost"/> instance.
/// It handles storing/restoring focus, suppressing input routed through the host's sibling scope,
/// and disabling/restoring sibling UI elements while a dialog is active.
/// </summary>
/// <remarks>
/// This type is internal and intended to be used from the UI thread only. Members are not thread-safe and
/// callers should invoke its methods on the host Dispatcher.
/// </remarks>
internal sealed class ContentDialogHostController
{
    private readonly DependencyObject _host;
    private readonly HashSet<DependencyObject> _disabledElements = [];
    private DependencyObject? _inputScope;
    private bool _siblingsIsDisabled;
    private bool _dialogActive;

    // Previously focused element before the dialog was shown. Restored after dialog is closed.
    private IInputElement? _previousFocusedElement;

    // ReSharper disable once ReplaceWithFieldKeyword
    // Backing field for IsDisableSiblingsEnabled property.
    private bool _isDisableSiblingsEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogHostController"/> class for the specified host element.
    /// </summary>
    /// <param name="host">The dependency object that acts as the dialog host (typically a <see cref="ContentDialogHost"/>).</param>
    public ContentDialogHostController(DependencyObject host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// Gets or sets a value indicating whether sibling elements of the dialog host should be disabled
    /// while the dialog is displayed.
    /// </summary>
    public bool IsDisableSiblingsEnabled
    {
        get => _isDisableSiblingsEnabled;
        set
        {
            if (_isDisableSiblingsEnabled == value)
            {
                return;
            }

            _isDisableSiblingsEnabled = value;

            if (!_dialogActive)
            {
                return;
            }

            if (_isDisableSiblingsEnabled && !_siblingsIsDisabled)
            {
                DisableHostSiblings();
            }
            else if (!_isDisableSiblingsEnabled && _siblingsIsDisabled)
            {
                RestoreDialogHostSiblings();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a dialog is currently active in this ContentDialogHost.
    /// </summary>
    public bool IsDialogActive => _dialogActive;

    /// <summary>
    /// Called when a dialog is added to the host. Stores the previously focused element, blocks scope-level input
    /// and disables siblings (if configured).
    /// </summary>
    public void HandleDialogAdded()
    {
        if (_dialogActive)
        {
            return;
        }

        StorePreviousFocus();
        BlockHostScopeInput();

        if (IsDisableSiblingsEnabled && !_siblingsIsDisabled)
        {
            DisableHostSiblings();
        }

        _dialogActive = true;
    }

    /// <summary>
    /// Called when a dialog is removed from the host. Restores sibling state, scope input and focus.
    /// </summary>
    public void HandleDialogRemoved()
    {
        if (!_dialogActive)
        {
            return;
        }

        if (_siblingsIsDisabled)
        {
            RestoreDialogHostSiblings();
        }

        UnblockHostScopeInput();
        RestorePreviousFocus();

        _dialogActive = false;
    }

    private void StorePreviousFocus()
    {
        IInputElement? focused = Keyboard.FocusedElement;
        if (IsValidInputElement(focused))
        {
            _previousFocusedElement = focused;
        }
        else
        {
            _previousFocusedElement = null;
        }
    }

    private void RestorePreviousFocus()
    {
        if (_previousFocusedElement != null && _host is DispatcherObject dispatcherObject)
        {
            _ = dispatcherObject.Dispatcher.BeginInvoke(
                () =>
                {
                    if (
                        dispatcherObject.Dispatcher
                            is { HasShutdownStarted: false, HasShutdownFinished: false }
                        && IsValidInputElement(_previousFocusedElement)
                    )
                    {
                        _previousFocusedElement.Focus();
                    }

                    _previousFocusedElement = null;
                },
                DispatcherPriority.Input
            );
        }
    }

    private static bool IsValidInputElement(IInputElement? element)
    {
        return element is UIElement or ContentElement or UIElement3D;
    }

    private void DisableHostSiblings()
    {
        _disabledElements.Clear();

        var parent = _host.GetParent();
        if (parent == null)
        {
            return;
        }

        switch (parent)
        {
            case Panel panel:
            {
                foreach (var child in panel.Children.OfType<UIElement>())
                {
                    DisableNonHostElement(child);
                }

                break;
            }

            case ContentControl contentControl:
            {
                if (contentControl.Content is DependencyObject content)
                {
                    DisableNonHostElement(content);
                }

                break;
            }

            case Decorator decorator:
            {
                if (decorator.Child is DependencyObject child)
                {
                    DisableNonHostElement(child);
                }

                break;
            }

            case ItemsControl itemsControl:
            {
                foreach (var item in itemsControl.Items)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item);
                    if (container != null)
                    {
                        DisableNonHostElement(container);
                    }
                }

                break;
            }

            case Visual
            or Visual3D:
            {
                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    DisableNonHostElement(child);
                }

                break;
            }

            default:
            {
                foreach (var obj in LogicalTreeHelper.GetChildren(parent).OfType<object>())
                {
                    if (obj is DependencyObject d)
                    {
                        DisableNonHostElement(d);
                    }
                }

                break;
            }
        }

        _siblingsIsDisabled = true;
    }

    private void RestoreDialogHostSiblings()
    {
        foreach (var obj in _disabledElements)
        {
            if (obj is TitleBar titleBar)
            {
                titleBar.IsUserContentEnabled = true;
                continue;
            }

            switch (obj)
            {
                case UIElement ui:
                    ui.SetCurrentValue(UIElement.IsEnabledProperty, true);
                    break;

                case FrameworkContentElement fce:
                    fce.SetCurrentValue(ContentElement.IsEnabledProperty, true);
                    break;
            }
        }

        _disabledElements.Clear();
        _siblingsIsDisabled = false;
    }

    private void DisableNonHostElement(DependencyObject? d)
    {
        if (d == null)
        {
            return;
        }

        if (IsPartOfHost(d) || ReferenceEquals(d, _host))
        {
            return;
        }

        // Special handling for the TitleBar
        if (d is TitleBar titleBar)
        {
            try
            {
                titleBar.IsUserContentEnabled = false;
                _disabledElements.Add(d);
            }
            catch
            {
                // Ignore single element disable failure.
            }

            return;
        }

        try
        {
            switch (d)
            {
                case UIElement { IsEnabled: true } ui:
                    _disabledElements.Add(d);
                    ui.SetCurrentValue(UIElement.IsEnabledProperty, false);
                    break;

                case FrameworkContentElement { IsEnabled: true } fce:
                    _disabledElements.Add(d);
                    fce.SetCurrentValue(ContentElement.IsEnabledProperty, false);
                    break;
            }
        }
        catch
        {
            // Ignore single element disable failure.
        }
    }

    private void BlockHostScopeInput()
    {
        if (_inputScope is not null)
        {
            return;
        }

        // The immediate parent defines the same scope whose children are disabled below. Routed
        // command and access-key handlers attached here see input from both the dialog and its
        // siblings. Unlike mutating Window.InputBindings, this remains valid for ElementHost and
        // HwndSource roots and lets independent plug-in regions share one native/WPF window.
        DependencyObject? scope = _host.GetParent();
        if (scope is null)
        {
            return;
        }

        AccessKeyManager.AddAccessKeyPressedHandler(scope, HostAccessKeySuppressHandler);
        if (scope is UIElement uiScope)
        {
            CommandManager.AddPreviewExecutedHandler(uiScope, PreviewExecutedSuppressHandler);
            CommandManager.AddPreviewCanExecuteHandler(uiScope, PreviewCanExecuteSuppressHandler);
        }

        _inputScope = scope;
    }

    private void UnblockHostScopeInput()
    {
        if (_inputScope is not { } scope)
        {
            return;
        }

        AccessKeyManager.RemoveAccessKeyPressedHandler(scope, HostAccessKeySuppressHandler);
        if (scope is UIElement uiScope)
        {
            CommandManager.RemovePreviewExecutedHandler(uiScope, PreviewExecutedSuppressHandler);
            CommandManager.RemovePreviewCanExecuteHandler(uiScope, PreviewCanExecuteSuppressHandler);
        }

        _inputScope = null;
    }

    private bool IsPartOfHost(DependencyObject d)
    {
        var current = d;
        while (current != null)
        {
            if (ReferenceEquals(current, _host))
            {
                return true;
            }

            current = current.GetParent();
        }

        return false;
    }

    private void HostAccessKeySuppressHandler(object sender, AccessKeyPressedEventArgs e)
    {
        var target = e.Target;
        if (target is null)
        {
            return;
        }

        if (IsPartOfHost(target))
        {
            return;
        }

        e.Target = null;
    }

    private void PreviewExecutedSuppressHandler(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && !IsPartOfHost(d))
        {
            e.Handled = true;
        }
    }

    private void PreviewCanExecuteSuppressHandler(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && !IsPartOfHost(d))
        {
            e.CanExecute = false;
            e.Handled = true;
        }
    }
}

#pragma warning restore IDE0008 // Use explicit type instead of 'var'
