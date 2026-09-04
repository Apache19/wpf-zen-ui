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
/// Retains native Button invocation and adds independent expansion for assistive technology.
/// Expanding the flyout does not execute the button command.
/// </summary>
internal sealed class DropDownButtonAutomationPeer(DropDownButton owner)
    : ButtonAutomationPeer(owner), IExpandCollapseProvider
{
    private ExpandCollapseState _lastState = GetState(owner);

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(DropDownButton);
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.ExpandCollapse && owner.Flyout is not null
            ? this
            : base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState
    {
        get => GetState(owner);
    }

    private static ExpandCollapseState GetState(DropDownButton button)
    {
        return button.Flyout is null
               ? ExpandCollapseState.LeafNode
               : button.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    }

    /// <inheritdoc />
    public void Expand()
    {
        SetExpanded(true);
    }

    /// <inheritdoc />
    public void Collapse()
    {
        SetExpanded(false);
    }

    private void SetExpanded(bool expanded)
    {
        if (!owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (owner.Flyout is null)
        {
            throw new InvalidOperationException("The button has no flyout.");
        }

        owner.SetCurrentValue(DropDownButton.IsDropDownOpenProperty, expanded);
    }

    /// <summary>Notifies existing automation clients without creating a peer on ordinary state changes.</summary>
    internal static void RaiseStateChanged(DropDownButton button)
    {
        if (FromElement(button) is DropDownButtonAutomationPeer peer && peer._lastState != peer.ExpandCollapseState)
        {
            ExpandCollapseState oldState = peer._lastState;
            peer._lastState = peer.ExpandCollapseState;
            peer.RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldState,
                peer._lastState);
        }
    }
}
