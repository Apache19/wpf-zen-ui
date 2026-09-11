// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Implements the built-in target contract for WPF's three editable text-control families.
/// </summary>
internal sealed class SoftKeyboardTargetAdapter : ISoftKeyboardTargetAdapter
{
    internal static SoftKeyboardTargetAdapter Default { get; } = new();

    /// <inheritdoc />
    public bool IsSupported(IInputElement target) => target is TextBoxBase or PasswordBox;

    /// <inheritdoc />
    public bool CanReceiveInput(IInputElement target)
    {
        if (target is not UIElement element
            || !element.IsEnabled
            || !element.IsVisible
            || !element.Focusable)
        {
            return false;
        }

        if (target is PasswordBox)
        {
            return true;
        }

        return target is TextBoxBase textBox && !textBox.IsReadOnly;
    }

    /// <inheritdoc />
    public InputScopeNameValue GetInputScope(IInputElement target)
    {
        if (target is not DependencyObject dependencyObject)
        {
            return InputScopeNameValue.Default;
        }

        InputScope? scope = InputMethod.GetInputScope(dependencyObject);
        return scope?.Names.Count > 0 && scope.Names[0] is InputScopeName name
            ? name.NameValue
            : InputScopeNameValue.Default;
    }

    /// <inheritdoc />
    public bool TryFocus(IInputElement target)
    {
        return ReferenceEquals(Keyboard.FocusedElement, target)
            || ReferenceEquals(Keyboard.Focus(target), target);
    }
}
