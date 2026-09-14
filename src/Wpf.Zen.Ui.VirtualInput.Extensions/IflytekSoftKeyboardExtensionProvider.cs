// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Adapts iFlytek's native input mode.
/// </summary>
/// <remarks>
/// iFlytek 3.0 uses keyboard-open values 1 and 2 for English and Chinese respectively, while its
/// conversion compartment remains alphanumeric. Its language-bar command updates the engine as
/// well as the compartment; writing WPF's Boolean IME state alone does not perform that transition.
/// </remarks>
public sealed class IflytekSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Gets the TSF service identifier for iFlytek's desktop input method.
    /// </summary>
    public static Guid ServiceId { get; } = new("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643");

    /// <summary>
    /// Gets the shared provider included in the default extension composition.
    /// </summary>
    public static IflytekSoftKeyboardExtensionProvider Default { get; } = new();

    private static readonly SoftKeyboardExtension Extension = new(
        inputStateAdapter: new InputStateAdapter()
    );

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Layout.InputMethod?.ClassId == ServiceId ? [Extension] : [];
    }

    /// <summary>
    /// Decodes iFlytek's native mode without collapsing its two open states to a Boolean.
    /// </summary>
    internal static InputMethodState DecodeOpenValue(int? value)
    {
        return value switch
        {
            2      => InputMethodState.On,
            0 or 1 => InputMethodState.Off,
            _      => InputMethodState.DoNotCare,
        };
    }

    private sealed class InputStateAdapter : ISoftKeyboardInputStateAdapter
    {
        /// <inheritdoc />
        public InputMethodState GetState(SoftKeyboardInputMethod? inputMethod)
        {
            return inputMethod?.ClassId == ServiceId
                ? DecodeOpenValue(SoftKeyboardTsfInputMode.GetKeyboardOpenValue())
                : InputMethodState.DoNotCare;
        }

        /// <inheritdoc />
        public bool TrySetState(SoftKeyboardInputMethod? inputMethod, InputMethodState state)
        {
            if (state is not (InputMethodState.On or InputMethodState.Off))
            {
                return false;
            }

            var current = GetState(inputMethod);
            return current is InputMethodState.On or InputMethodState.Off
                   && (current == state || SoftKeyboardTsfInputMode.TryToggleInputMode(ServiceId));
        }
    }
}
