// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;

namespace Wpf.Zen.Ui.VirtualInput.AutomationPeers;

/// <summary>
/// Exposes <see cref="SoftKeyboard"/> as a keyboard pane without moving keyboard focus.
/// </summary>
/// <remarks>
/// Individual keys are ordinary button peers supplied by the template. They implement Invoke and
/// remain non-focusable. WPF reports the visibility of the composite surface.
/// </remarks>
internal sealed class SoftKeyboardAutomationPeer : FrameworkElementAutomationPeer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The keyboard represented by the peer.</param>
    public SoftKeyboardAutomationPeer(SoftKeyboard owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(SoftKeyboard);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Pane;
    }
}
