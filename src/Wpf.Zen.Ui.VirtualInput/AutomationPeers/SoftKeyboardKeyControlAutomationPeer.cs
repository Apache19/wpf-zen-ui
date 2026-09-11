// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.VirtualInput.AutomationPeers;

/// <summary>
/// Exposes a keyboard cap as an invokable button while keeping input execution in its host.
/// </summary>
public sealed class SoftKeyboardKeyControlAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardKeyControlAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The key exposing semantic input requests.</param>
    public SoftKeyboardKeyControlAutomationPeer(SoftKeyboardKeyControl owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Invoke
            ? this
            : base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(SoftKeyboardKeyControl);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Button;
    }

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();

        return string.IsNullOrEmpty(name)
            ? ((SoftKeyboardKeyControl)Owner).Key?.AutomationName ?? string.Empty
            : name;
    }

    void IInvokeProvider.Invoke()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        // Match WPF Invoke scheduling so clients do not block on input handlers. The control
        // rechecks its lifetime and enabled state when the queued request actually executes.
        _ = Owner.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(((SoftKeyboardKeyControl)Owner).Invoke)
        );
    }
}