// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.AutomationPeers;

/// <summary>
/// Exposes <see cref="SoftKeyboard"/> as an expandable keyboard surface without moving keyboard focus.
/// </summary>
/// <remarks>
/// Individual keys are ordinary button peers supplied by the template. They implement Invoke and
/// remain non-focusable, while this peer supplies the open/closed state of the composite surface.
/// </remarks>
internal sealed class SoftKeyboardAutomationPeer : FrameworkElementAutomationPeer, IExpandCollapseProvider
{
    private readonly SoftKeyboard _owner;

    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardAutomationPeer"/> class.</summary>
    /// <param name="owner">The keyboard represented by the peer.</param>
    public SoftKeyboardAutomationPeer(SoftKeyboard owner)
        : base(owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(SoftKeyboard);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState => _owner.IsOpen
        ? ExpandCollapseState.Expanded
        : ExpandCollapseState.Collapsed;

    void IExpandCollapseProvider.Expand()
    {
        EnsureEnabled();
        _owner.SetCurrentValue(SoftKeyboard.IsOpenProperty, true);
    }

    void IExpandCollapseProvider.Collapse()
    {
        EnsureEnabled();
        _owner.SetCurrentValue(SoftKeyboard.IsOpenProperty, false);
    }

    /// <summary>Raises the standardized open-state notification when <see cref="SoftKeyboard.IsOpen"/> changes.</summary>
    internal static void RaiseIsOpenChanged(SoftKeyboard owner, bool oldValue, bool newValue)
    {
        if (oldValue == newValue || FromElement(owner) is not SoftKeyboardAutomationPeer peer)
        {
            return;
        }

        peer.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
        );
    }

    private void EnsureEnabled()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }
    }
}
