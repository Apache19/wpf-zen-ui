// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DateAndTime;

/// <summary>
/// 管理日历示例的显示日期、禁选开关及禁选范围说明。
/// </summary>
public partial class CalendarViewModel : ViewModel
{
    private DateTime _displayDate = DateTime.Today;
    private bool _isBlackoutEnabled;
    private CalendarDateRange? _blackoutRange;

    /// <summary>
    /// 获取或设置日历当前显示的日期，默认是今天，用于确定启用禁选时的月份。
    /// </summary>
    public DateTime DisplayDate
    {
        get => _displayDate;
        set => SetProperty(ref _displayDate, value);
    }

    /// <summary>
    /// 获取或设置是否禁选当前显示月份的 10 至 12 日，默认关闭。
    /// </summary>
    /// <remarks>
    /// 启用时确定日期范围，随后翻月不会改变该范围；关闭后可重新启用以选择新的月份。
    /// </remarks>
    public bool IsBlackoutEnabled
    {
        get => _isBlackoutEnabled;
        set
        {
            if (!SetProperty(ref _isBlackoutEnabled, value))
            {
                return;
            }

            if (value)
            {
                var start = new DateTime(DisplayDate.Year, DisplayDate.Month, 10);
                var end = start.AddDays(2);
                BlackoutRange = new CalendarDateRange(start, end);
            }
            else
            {
                BlackoutRange = null;
            }
        }
    }

    /// <summary>
    /// 获取当前禁选范围；未启用时为 null，由视图适配到日历的禁选集合。
    /// </summary>
    public CalendarDateRange? BlackoutRange
    {
        get => _blackoutRange;
        private set
        {
            if (SetProperty(ref _blackoutRange, value))
            {
                OnPropertyChanged(nameof(BlackoutDatesDescription));
            }
        }
    }

    /// <summary>
    /// 获取禁选范围或启用说明，供属性面板展示。
    /// </summary>
    public string BlackoutDatesDescription
    {
        get
        {
            var range = BlackoutRange;
            if (range is null)
            {
                return "Block the 10th–12th of the displayed month. An overlapping selection will be cleared.";
            }

            return $"Blocked: {range.Start:yyyy-MM-dd} – {range.End:yyyy-MM-dd}.";
        }
    }
}
