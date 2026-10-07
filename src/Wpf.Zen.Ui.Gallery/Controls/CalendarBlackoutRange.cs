// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Calendar = System.Windows.Controls.Calendar;

namespace Wpf.Zen.Ui.Gallery.Controls;

/// <summary>
/// 将绑定的单个禁选范围同步到日历的只读 BlackoutDates 集合。
/// </summary>
/// <remarks>
/// 此适配器管理目标日历的整个禁选集合，适用于图库中的单范围示例。
/// 与新范围重叠的选择会整体清空，以保持 SingleRange 模式的连续性。
/// </remarks>
public static class CalendarBlackoutRange
{
    /// <summary>
    /// 标识 Range 附加属性；默认 null，表示没有禁选日期。
    /// </summary>
    public static readonly DependencyProperty RangeProperty = DependencyProperty.RegisterAttached(
        "Range",
        typeof(CalendarDateRange),
        typeof(CalendarBlackoutRange),
        new PropertyMetadata(null, OnRangeChanged));

    /// <summary>
    /// 获取要同步到日历的禁选范围。
    /// </summary>
    /// <param name="element">附加属性所在的对象。</param>
    /// <returns>禁选范围；null 表示没有禁选日期。</returns>
    public static CalendarDateRange? GetRange(DependencyObject element)
    {
        return (CalendarDateRange?)element.GetValue(RangeProperty);
    }

    /// <summary>
    /// 设置要同步到日历的禁选范围；更新范围时应替换整个对象。
    /// </summary>
    /// <param name="element">目标日历。</param>
    /// <param name="value">禁选范围，或用于清除禁选日期的 null。</param>
    public static void SetRange(DependencyObject element, CalendarDateRange? value)
    {
        element.SetValue(RangeProperty, value);
    }

    /// <summary>
    /// 在应用禁选范围前处理 WPF 的选择冲突约束，并同步禁选集合。
    /// </summary>
    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Calendar calendar)
        {
            return;
        }

        calendar.BlackoutDates.Clear();
        if (e.NewValue is not CalendarDateRange range)
        {
            return;
        }

        // WPF 会拒绝包含已选日期的禁选范围，因此必须先清理冲突选择。
        var hasOverlap = false;
        foreach (var date in calendar.SelectedDates)
        {
            if (date >= range.Start && date <= range.End)
            {
                hasOverlap = true;
                break;
            }
        }

        if (hasOverlap)
        {
            calendar.SelectedDates.Clear();
        }

        calendar.BlackoutDates.Add(range);
    }
}
