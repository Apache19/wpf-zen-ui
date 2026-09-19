// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Configures service-specific interpretation of the current observation.
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
    /// Gets or sets the adapter that contributes observed presentation and layout policies.
    /// </summary>
    /// <remarks>
    /// The default is null, which selects the first adapter from the configured extensions.
    /// This property is not inherited or animated. Changes reinterpret existing observations,
    /// respecting any activation window already in progress. Platform commands retain priority;
    /// an adapter can explicitly override their button content without changing execution routing.
    /// </remarks>
    public ISoftKeyboardInputModeAdapter? InputModeAdapter
    {
        get => (ISoftKeyboardInputModeAdapter?)GetValue(InputModeAdapterProperty);
        set => SetValue(InputModeAdapterProperty, value);
    }
}
