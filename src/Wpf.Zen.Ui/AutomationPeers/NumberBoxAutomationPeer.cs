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
/// Exposes <see cref="NumberBox"/> as an editable spinner while preserving the inherited text and
/// value automation patterns used for direct textual input.
/// </summary>
/// <remarks>
/// WPF has no native numeric text editor. A plain <see cref="TextBoxAutomationPeer"/> therefore
/// reports only editable text and omits the range, bounds, and step sizes. Adding
/// <see cref="IRangeValueProvider"/> lets assistive technology adjust the numeric value without
/// depending on template buttons, which are optional implementation details.
/// </remarks>
internal sealed class NumberBoxAutomationPeer : TextBoxAutomationPeer, IRangeValueProvider
{
    private readonly NumberBox _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberBoxAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The number box represented by this peer.</param>
    public NumberBoxAutomationPeer(NumberBox owner)
        : base(owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(NumberBox);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Spinner;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.RangeValue
            ? this
            : base.GetPattern(patternInterface);
    }

    bool IRangeValueProvider.IsReadOnly => !_owner.IsEnabled || _owner.IsReadOnly;

    double IRangeValueProvider.LargeChange => _owner.LargeChange;

    double IRangeValueProvider.Maximum => _owner.Maximum;

    double IRangeValueProvider.Minimum => _owner.Minimum;

    double IRangeValueProvider.SmallChange => _owner.SmallChange;

    double IRangeValueProvider.Value => GetAutomationValue(_owner, _owner.Value);

    void IRangeValueProvider.SetValue(double value)
    {
        if (!_owner.IsEnabled || _owner.IsReadOnly)
        {
            throw new ElementNotEnabledException();
        }

        if (double.IsNaN(value) || double.IsInfinity(value)
                                || value < _owner.Minimum
                                || value > _owner.Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        // SetCurrentValue preserves a consumer-provided binding, matching NumberBox's input and
        // spin paths. UI Automation must never replace the binding with a local value.
        _owner.SetCurrentValue(NumberBox.ValueProperty, value);
    }

    /// <summary>Raises a RangeValue notification for an effective numeric-value change.</summary>
    internal static void RaiseValuePropertyChangedEvent(
        NumberBox owner,
        double? oldValue,
        double? newValue
    )
    {
        if (UIElementAutomationPeer.FromElement(owner) is not NumberBoxAutomationPeer peer)
        {
            return;
        }

        double oldAutomationValue = GetAutomationValue(owner, oldValue);
        double newAutomationValue = GetAutomationValue(owner, newValue);
        if (!oldAutomationValue.Equals(newAutomationValue))
        {
            peer.RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                oldAutomationValue,
                newAutomationValue
            );
        }
    }

    /// <summary>Raises a RangeValue notification for an effective minimum change.</summary>
    internal static void RaiseMinimumPropertyChangedEvent(NumberBox owner, double oldValue, double newValue)
    {
        RaisePropertyChangedEvent(
            owner,
            RangeValuePatternIdentifiers.MinimumProperty,
            oldValue,
            newValue
        );

        // A nullable NumberBox has no numeric RangeValue representation and is exposed as Minimum.
        // Keep the provider's derived Value observable when that fallback moves.
        if (owner.Value is null)
        {
            RaisePropertyChangedEvent(
                owner,
                RangeValuePatternIdentifiers.ValueProperty,
                oldValue,
                newValue
            );
        }
    }

    /// <summary>Raises a RangeValue notification for an effective maximum change.</summary>
    internal static void RaiseMaximumPropertyChangedEvent(NumberBox owner, double oldValue, double newValue) =>
        RaisePropertyChangedEvent(owner, RangeValuePatternIdentifiers.MaximumProperty, oldValue, newValue);

    /// <summary>Raises a RangeValue notification for an effective small-step change.</summary>
    internal static void RaiseSmallChangePropertyChangedEvent(NumberBox owner, double oldValue, double newValue) =>
        RaisePropertyChangedEvent(owner, RangeValuePatternIdentifiers.SmallChangeProperty, oldValue, newValue);

    /// <summary>Raises a RangeValue notification for an effective large-step change.</summary>
    internal static void RaiseLargeChangePropertyChangedEvent(NumberBox owner, double oldValue, double newValue) =>
        RaisePropertyChangedEvent(owner, RangeValuePatternIdentifiers.LargeChangeProperty, oldValue, newValue);

    /// <summary>Raises a RangeValue notification when editing becomes read-only or writable.</summary>
    internal static void RaiseIsReadOnlyPropertyChangedEvent(NumberBox owner, bool oldValue, bool newValue) =>
        RaisePropertyChangedEvent(owner, RangeValuePatternIdentifiers.IsReadOnlyProperty, oldValue, newValue);

    private static double GetAutomationValue(NumberBox owner, double? value)
    {
        // RangeValue has no nullable representation. Minimum is the only value that is guaranteed
        // to satisfy the provider contract for every valid range; the inherited text Value pattern
        // continues to expose the distinction between an empty editor and formatted minimum text.
        return value ?? owner.Minimum;
    }

    private static void RaisePropertyChangedEvent(
        NumberBox owner,
        AutomationProperty property,
        double oldValue,
        double newValue
    )
    {
        if (oldValue.Equals(newValue)
            || UIElementAutomationPeer.FromElement(owner) is not NumberBoxAutomationPeer peer)
        {
            return;
        }

        peer.RaisePropertyChangedEvent(property, oldValue, newValue);
    }

    private static void RaisePropertyChangedEvent(
        NumberBox owner,
        AutomationProperty property,
        bool oldValue,
        bool newValue
    )
    {
        if (oldValue == newValue
            || UIElementAutomationPeer.FromElement(owner) is not NumberBoxAutomationPeer peer)
        {
            return;
        }

        peer.RaisePropertyChangedEvent(property, oldValue, newValue);
    }
}
