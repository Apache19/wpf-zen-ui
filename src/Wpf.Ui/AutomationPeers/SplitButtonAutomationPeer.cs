// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Wpf.Ui.AutomationPeers;

/// <summary>
/// Exposes <see cref="SplitButton"/> as one keyboard-focusable automation element with independent
/// invoke and expand/collapse capabilities.
/// </summary>
/// <remarks>
/// The control template uses two real buttons to obtain WPF mouse and command behavior, but those
/// buttons are implementation details and cannot receive keyboard focus. Publishing them as two
/// additional control elements would make the automation tree disagree with keyboard navigation
/// and announce the same split button more than once, so <see cref="GetChildrenCore"/> filters the
/// two template-part peers while leaving rich content peers available through the normal tree.
/// </remarks>
internal sealed class SplitButtonAutomationPeer
    : FrameworkElementAutomationPeer, IInvokeProvider, IExpandCollapseProvider
{
    private readonly SplitButton _owner;

    /// <summary>Initializes a new instance of the <see cref="SplitButtonAutomationPeer"/> class.</summary>
    /// <param name="owner">The split button represented by this peer.</param>
    public SplitButtonAutomationPeer(SplitButton owner)
        : base(owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(SplitButton);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        // UI Automation has no dedicated SplitButton control type. Button plus Invoke and
        // ExpandCollapse patterns is the established composite contract understood by clients.
        return AutomationControlType.Button;
    }

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        string name = base.GetNameCore() ?? string.Empty;
        if (name.Length == 0)
        {
            name = AutomationProperties.GetName(_owner);
        }

        if (name.Length == 0 && _owner.Content is DependencyObject content)
        {
            name = AutomationProperties.GetName(content);
        }

        if (name.Length == 0 && _owner.Content is string text)
        {
            name = text;
        }

        return name;
    }

    /// <inheritdoc />
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        List<AutomationPeer>? children = base.GetChildrenCore();
        children?.RemoveAll(
            peer => peer is ButtonBaseAutomationPeer buttonPeer
                && _owner.IsTemplateButton(buttonPeer.Owner)
        );
        return children;
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Invoke)
        {
            return this;
        }

        if (patternInterface == PatternInterface.ExpandCollapse && _owner.Flyout is not null)
        {
            return this;
        }

        return base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState =>
        _owner.Flyout is null
            ? ExpandCollapseState.LeafNode
            : _owner.IsDropDownOpen
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    void IInvokeProvider.Invoke()
    {
        // Applying the template is necessary for command availability because the primary Button
        // owns WPF's ICommand.CanExecute subscription and contributes it to IsEnabledCore.
        _owner.ApplyTemplate();
        if (!_owner.CanInvokePrimaryAction)
        {
            throw new ElementNotEnabledException();
        }

        // Match WPF ButtonAutomationPeer: command handlers may open a modal window, so provider
        // calls must not synchronously block the UI Automation client.
        _ = _owner.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => _owner.InvokePrimaryAction())
        );
    }

    /// <inheritdoc />
    void IExpandCollapseProvider.Expand()
    {
        EnsureDropDownAvailable();
        _owner.OpenDropDown();
    }

    /// <inheritdoc />
    void IExpandCollapseProvider.Collapse()
    {
        EnsureDropDownAvailable();
        _owner.CloseDropDown();
    }

    /// <summary>Raises an Invoke notification for primary actions from any input source.</summary>
    internal static void RaiseInvokeAutomationEvent(SplitButton owner)
    {
        if (
            AutomationPeer.ListenerExists(AutomationEvents.InvokePatternOnInvoked)
            && UIElementAutomationPeer.FromElement(owner) is SplitButtonAutomationPeer peer
        )
        {
            peer.RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
        }
    }

    /// <summary>
    /// Raises an ExpandCollapse property notification when the ContextMenu confirms a state change.
    /// </summary>
    internal static void RaiseExpandCollapseAutomationEvent(
        SplitButton owner,
        bool oldIsOpen,
        bool newIsOpen
    )
    {
        if (
            oldIsOpen == newIsOpen
            || UIElementAutomationPeer.FromElement(owner) is not SplitButtonAutomationPeer peer
        )
        {
            return;
        }

        peer.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldIsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newIsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
        );
    }

    private void EnsureDropDownAvailable()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (_owner.Flyout is null)
        {
            throw new InvalidOperationException("A split button without a flyout cannot be expanded or collapsed.");
        }
    }
}
