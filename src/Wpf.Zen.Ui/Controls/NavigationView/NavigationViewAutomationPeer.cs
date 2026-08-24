// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Exposes the single-selection contract owned by <see cref="NavigationView"/> to UI Automation.
/// </summary>
/// <remarks>
/// Item peers expose SelectionItem and return this peer as their selection container. Providing the
/// matching Selection pattern here keeps that relationship valid without changing NavigationView
/// into a WPF Selector, whose item and selection semantics do not match the hierarchical menu.
/// </remarks>
internal sealed class NavigationViewAutomationPeer
    : FrameworkElementAutomationPeer, ISelectionProvider
{
    private readonly NavigationView _owner;

    /// <summary>Initializes a new instance of the <see cref="NavigationViewAutomationPeer"/> class.</summary>
    /// <param name="owner">The control that owns navigation selection.</param>
    internal NavigationViewAutomationPeer(NavigationView owner)
        : base(owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(NavigationView);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Pane;
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.Selection
            ? this
            : base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    bool ISelectionProvider.CanSelectMultiple
    {
        get => false;
    }

    /// <inheritdoc />
    bool ISelectionProvider.IsSelectionRequired
    {
        get => false;
    }

    /// <inheritdoc />
    IRawElementProviderSimple[] ISelectionProvider.GetSelection()
    {
        var selectedContainer = _owner.GetSelectedNavigationContainer();
        if (selectedContainer is null)
        {
            return [];
        }

        var peer = CreatePeerForElement(selectedContainer);
        return peer is null ? [] : [ProviderFromPeer(peer)];
    }
}
