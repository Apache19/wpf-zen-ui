// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Synchronizes native input mode without transferring ownership of the editor document.
/// </summary>
public partial class SoftKeyboard
{
    /// <summary>
    /// Identifies the <see cref="InputModeAdapter"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputModeAdapterProperty
        = DependencyProperty.Register(
            nameof(InputModeAdapter),
            typeof(ISoftKeyboardInputModeAdapter),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                null,
                static (owner, _) => ((SoftKeyboard)owner).RefreshInputMode()
            )
        );

    /// <summary>
    /// Gets or sets the adapter that interprets the active text service's input modes.
    /// </summary>
    /// <remarks>
    /// The default is null, which uses the input-method source's standard TSF interpretation.
    /// This property is not inherited or animated.
    /// Changing it immediately performs a complete observation and may regenerate the layout.
    /// Adapter read failures preserve the last valid mode. A failed adapter transition is never
    /// followed by a second transition through another mechanism.
    /// </remarks>
    public ISoftKeyboardInputModeAdapter? InputModeAdapter
    {
        get => (ISoftKeyboardInputModeAdapter?)GetValue(InputModeAdapterProperty);
        set => SetValue(InputModeAdapterProperty, value);
    }

}
