// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// 向辅助技术暴露完整日期时间文本和弹出层展开状态。
/// </summary>
internal sealed class DateTimePickerAutomationPeer : FrameworkElementAutomationPeer,
    IValueProvider, IExpandCollapseProvider
{
    internal DateTimePickerAutomationPeer(DateTimePicker owner) : base(owner)
    {
    }

    private DateTimePicker Picker => (DateTimePicker)Owner;
    /// <inheritdoc />
    public bool IsReadOnly => !IsEnabled();
    /// <inheritdoc />
    public string Value => Picker.Text;
    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState => Picker.IsDropDownOpen
        ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Value || patternInterface == PatternInterface.ExpandCollapse)
        {
            return this;
        }
        return base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(DateTimePicker);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.ComboBox;
    }

    /// <inheritdoc />
    public void SetValue(string value)
    {
        EnsureEnabled();
        Picker.SetCurrentValue(DateTimePicker.TextProperty, value);
    }

    /// <inheritdoc />
    public void Expand()
    {
        EnsureEnabled();
        Picker.SetCurrentValue(DateTimePicker.IsDropDownOpenProperty, true);
    }

    /// <inheritdoc />
    public void Collapse()
    {
        EnsureEnabled();
        Picker.Cancel();
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }
    }
}