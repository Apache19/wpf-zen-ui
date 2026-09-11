// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.AutomationPeers;

/// <summary>Exposes a candidate container as an invokable button while keeping input execution in its host.</summary>
public sealed class SoftKeyboardCandidateItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardCandidateItemAutomationPeer"/> class.</summary>
    /// <param name="owner">The key exposing semantic input requests.</param>
    public SoftKeyboardCandidateItemAutomationPeer(SoftKeyboardCandidateItem owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(SoftKeyboardCandidateItem);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Button;
    }

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        // The generated candidate identity remains authoritative even when a style replaces Content.
        string name = AutomationProperties.GetName(Owner);
        if (AutomationProperties.GetLabeledBy(Owner) is not null)
        {
            return base.GetNameCore();
        }

        return string.IsNullOrEmpty(name)
               && ((SoftKeyboardCandidateItem)Owner).Candidate is SoftKeyboardCandidate candidate
            ? candidate.Text
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
            new Action(((SoftKeyboardCandidateItem)Owner).Invoke)
        );
    }
}