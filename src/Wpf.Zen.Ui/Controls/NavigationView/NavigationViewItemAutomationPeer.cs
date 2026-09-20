// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

internal class NavigationViewItemAutomationPeer
    : FrameworkElementAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
{
    private readonly NavigationViewItem _owner;

    public NavigationViewItemAutomationPeer(NavigationViewItem owner)
        : base(owner)
    {
        _owner = owner;
    }

    protected override string GetClassNameCore()
    {
        return "NavigationItem";
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.TabItem;
    }

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore()
    {
        return _owner.HeaderButton?.Focusable == true && _owner.HeaderButton.IsEnabled;
    }

    /// <inheritdoc />
    protected override bool HasKeyboardFocusCore()
    {
        return _owner.HeaderButton?.IsKeyboardFocused == true;
    }

    /// <inheritdoc />
    protected override void SetFocusCore()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (!_owner.FocusHeader())
        {
            throw new InvalidOperationException("The navigation item's header cannot receive keyboard focus.");
        }
    }

    /// <inheritdoc />
    protected override Rect GetBoundingRectangleCore()
    {
        // NavigationViewItem owns the inline child presenter, so its visual bounds grow when the
        // branch expands. UIA represents this peer as the selectable header row, however, and its
        // focus indicator must therefore use the bounded input surface rather than the whole branch.
        return GetHeaderButtonAutomationPeer()?.GetBoundingRectangle()
            ?? base.GetBoundingRectangleCore();
    }

    /// <inheritdoc />
    protected override Point GetClickablePointCore()
    {
        return GetHeaderButtonAutomationPeer()?.GetClickablePoint()
            ?? base.GetClickablePointCore();
    }

    /// <inheritdoc />
    protected override bool IsOffscreenCore()
    {
        return GetHeaderButtonAutomationPeer()?.IsOffscreen()
            ?? base.IsOffscreenCore();
    }

    /// <inheritdoc />
    /// <remarks>
    /// PART_HeaderButton is the implementation surface for the selection and expand/collapse
    /// patterns exposed by this peer. Returning its Button peer as an additional control would
    /// announce one navigation item twice. The Chevron is deliberately a non-interactive visual;
    /// rich header content and realized child-item peers remain available through the normal tree.
    /// </remarks>
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        List<AutomationPeer>? children = base.GetChildrenCore();
        if (children is null)
        {
            return null;
        }

        children.RemoveAll(peer =>
        {
            if (
                peer is not ButtonBaseAutomationPeer buttonPeer
                || !ReferenceEquals(buttonPeer.Owner, _owner.HeaderButton)
            )
            {
                return false;
            }

            ConfigureHeaderButtonAutomationPeer(buttonPeer);
            return true;
        });

        return children;
    }

    public override object? GetPattern(PatternInterface patternInterface)
    {
        // Only provide expand collapse pattern if we have children!
        // https://github.com/microsoft/microsoft-ui-xaml/blob/50177b54e88e923e24440df679bdf984b0048ab4/src/controls/dev/NavigationView/NavigationViewItemAutomationPeer.cpp#L52
        if (
            patternInterface == PatternInterface.SelectionItem ||
            (
                patternInterface == PatternInterface.ExpandCollapse && _owner is { HasItems: true }
            )
        )
        {
            return this;
        }

        return base.GetPattern(patternInterface);
    }

    public ExpandCollapseState ExpandCollapseState
    {
        get
        {
            if (!_owner.HasItems)
            {
                return ExpandCollapseState.LeafNode;
            }

            return _owner.IsChildMenuOpen
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;
        }
    }

    public void Collapse()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (!_owner.HasItems)
        {
            throw new InvalidOperationException("A navigation item without children cannot be collapsed.");
        }

        if (ExpandCollapseState == ExpandCollapseState.Collapsed)
        {
            return;
        }

        _owner.SetChildMenuOpen(false, focusFirstItem: false);
    }

    public void Expand()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (!_owner.HasItems)
        {
            throw new InvalidOperationException("A navigation item without children cannot be expanded.");
        }

        if (ExpandCollapseState == ExpandCollapseState.Expanded)
        {
            return;
        }

        _owner.SetChildMenuOpen(true, focusFirstItem: false);
    }

    /// <summary>
    /// Raises an automation notification for state changes originating from mouse, keyboard,
    /// bindings, or native menu dismissal. Keeping the notification at the dependency-property
    /// boundary avoids reporting changes only when UI Automation initiated them.
    /// </summary>
    internal static void RaiseExpandCollapseAutomationEvent(
        NavigationViewItem owner,
        ExpandCollapseState oldState,
        ExpandCollapseState newState)
    {
        if (
            oldState == newState ||
            UIElementAutomationPeer.FromElement(owner) is not NavigationViewItemAutomationPeer peer
        )
        {
            return;
        }

        peer.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldState,
            newState
        );
    }

    /// <summary>
    /// Reports selection changes at the read-only active-state boundary so mouse, keyboard,
    /// programmatic navigation, and UI Automation all publish the same provider transition.
    /// </summary>
    internal static void RaiseSelectionAutomationEvent(
        NavigationViewItem owner,
        bool oldValue,
        bool newValue)
    {
        if (oldValue == newValue
            || UIElementAutomationPeer.FromElement(owner) is not NavigationViewItemAutomationPeer peer)
        {
            return;
        }

        peer.RaisePropertyChangedEvent(
            SelectionItemPatternIdentifiers.IsSelectedProperty,
            oldValue,
            newValue
        );

        if (newValue)
        {
            peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
        }
    }

    protected override AutomationPeer? GetLabeledByCore()
    {
        // UIElementAutomationPeer gives an explicit AutomationProperties.LabeledBy relationship
        // precedence over inferred content. Only infer the header when no such relationship exists.
        if (base.GetLabeledByCore() is { } explicitLabel)
        {
            return explicitLabel;
        }

        if (_owner.Header is UIElement element)
        {
            return CreatePeerForElement(element);
        }

        return null;
    }

    protected override string GetNameCore()
    {
        string result = base.GetNameCore() ?? string.Empty;

        if (result == string.Empty)
        {
            result = AutomationProperties.GetName(_owner);
        }

        if (result == string.Empty && _owner.Header is DependencyObject d)
        {
            result = AutomationProperties.GetName(d);
        }

        if (result == string.Empty && _owner.Header is string s)
        {
            result = s;
        }

        return result;
    }

    void ISelectionItemProvider.AddToSelection()
    {
        // This is a single select control, so just select the item.
        ((ISelectionItemProvider)this).Select();
    }

    bool ISelectionItemProvider.IsSelected
    {
        get
        {
            if (NavigationView.GetNavigationParent(_owner) is not { } navigationView)
            {
                return false;
            }

            return navigationView.TryGetNavigationEntry(_owner, out NavigationEntry? entry)
                && ReferenceEquals(navigationView.SelectedItem, entry.Item);
        }
    }

    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer
    {
        get
        {
            if (NavigationView.GetNavigationParent(_owner) is not { } navigationView)
            {
                return null;
            }

            if (CreatePeerForElement(navigationView) is { } peer)
            {
                return ProviderFromPeer(peer);
            }

            return null;
        }
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        EnsureOwnerEnabled();

        if (NavigationView.GetNavigationParent(_owner) is not { } navigationView)
        {
            return;
        }

        if (
            !navigationView.TryGetNavigationEntry(_owner, out NavigationEntry? entry)
            || !ReferenceEquals(navigationView.SelectedItem, entry.Item)
        )
        {
            return;
        }

        navigationView.ClearSelectedItem();
    }

    void ISelectionItemProvider.Select()
    {
        EnsureOwnerEnabled();
        _ = _owner.InvokePrimaryAction();
    }

    /// <summary>
    /// Mirrors WPF's SelectorItemAutomationPeer contract: provider actions on a disabled control
    /// fail explicitly instead of silently mutating selection or submenu state.
    /// </summary>
    private void EnsureOwnerEnabled()
    {
        if (!_owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }
    }

    /// <summary>
    /// Gets the peer for the concrete header surface and aggregates its events into this semantic
    /// navigation-item peer.
    /// </summary>
    /// <remarks>
    /// The template button remains hidden from the public automation tree to prevent duplicate
    /// announcements. WPF still creates its peer when the button receives keyboard focus, so the
    /// event source must be configured anywhere this peer delegates header geometry, even before a
    /// UIA client has requested the navigation item's children.
    /// </remarks>
    private AutomationPeer? GetHeaderButtonAutomationPeer()
    {
        if (
            _owner.HeaderButton is not { } headerButton
            || UIElementAutomationPeer.CreatePeerForElement(headerButton) is not { } peer
        )
        {
            return null;
        }

        ConfigureHeaderButtonAutomationPeer(peer);
        return peer;
    }

    /// <summary>
    /// Redirects automation events from the hidden template implementation to the navigation item
    /// exposed to UIA clients.
    /// </summary>
    private void ConfigureHeaderButtonAutomationPeer(AutomationPeer peer)
    {
        // Keyboard focus belongs to the template button, but that implementation peer is omitted
        // from the public automation tree. WPF resolves focus changes through EventsSource after
        // creating the peer for the focused input element. Without this aggregation UIA receives
        // the hidden button as the focus source and cannot track the NavigationViewItem.
        peer.EventsSource = this;
    }
}
