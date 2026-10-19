// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Calendar = System.Windows.Controls.Calendar;
using TextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// 验证日期时间事务、文本、约束及实际模板部件的协作。
/// </summary>
[Collection(nameof(KeyboardFocusTestCollection))]
public class DateTimePickerTests
{
    /// <summary>
    /// 默认文案与格式分别跟随线程区域，显式语言和继承语言覆盖后可恢复自动模式。
    /// </summary>
    [Fact]
    public void Localization_DefaultUsesCurrentCultures_AndLanguageOverridesCanBeCleared()
    {
        WpfTestThread.Run(() =>
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                WithWindow((picker, window) =>
                {
                    var date = new DateTime(2026, 10, 9, 13, 45, 0);
                    picker.DateTimeFormat = "D";
                    picker.SelectedDateTime = date;
                    var button = Part<ButtonBase>(picker, "PART_Button");
                    var host = Part<ContentControl>(picker, "PART_CalendarHost");
                    var calendar = Assert.IsType<Calendar>(host.Content);
                    AssertCulture("fr-FR", "选择日期和时间");
                    picker.IsDropDownOpen = true;
                    window.UpdateLayout();
                    var column = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
                    var hourName = System.Windows.Automation.AutomationProperties.GetName(column);
                    Assert.Equal("时", hourName);
                    picker.Cancel();

                    string? message = null;
                    picker.DateValidationError += (_, args) => message = args.Exception.Message;
                    picker.Text = "invalid";
                    Assert.False(picker.CommitText());
                    Assert.StartsWith("无法根据", message);

                    picker.Language = XmlLanguage.GetLanguage("en-US");
                    AssertCulture("en-US", "Choose date and time");
                    picker.ClearValue(FrameworkElement.LanguageProperty);
                    AssertCulture("fr-FR", "选择日期和时间");
                    window.Language = XmlLanguage.GetLanguage("en-US");
                    AssertCulture("en-US", "Choose date and time");
                    window.ClearValue(FrameworkElement.LanguageProperty);
                    AssertCulture("fr-FR", "选择日期和时间");

                    void AssertCulture(string cultureName, string expectedName)
                    {
                        window.UpdateLayout();
                        var culture = CultureInfo.GetCultureInfo(cultureName);
                        var expectedText = date.ToString("D", culture);
                        var actualName = System.Windows.Automation.AutomationProperties.GetName(button);
                        Assert.Equal(expectedText, picker.Text);
                        Assert.Equal(expectedName, actualName);
                        Assert.Equal(culture.DateTimeFormat.FirstDayOfWeek, picker.FirstDayOfWeek);
                        Assert.Equal(cultureName, calendar.Language.GetSpecificCulture().Name);
                    }
                });
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        });
    }

    /// <summary>
    /// 即时检查草稿不提交数据或触发失败事件，配置变化及恢复会刷新状态。
    /// </summary>
    [Fact]
    public void InputError_TracksDraftConstraintsAndRecovery()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var original = new DateTime(2026, 10, 6, 12, 0, 0);
            picker.SelectedDateTime = original;
            var input = Part<TextBox>(picker, "PART_TextBox");
            int errors = 0;
            picker.DateValidationError += (_, _) => errors++;
            Assert.True(DateTimePicker.HasInputErrorProperty.ReadOnly);
            input.Text = "invalid";
            Assert.True(picker.HasInputError);
            Assert.Equal(0, errors);
            Assert.Equal(original, picker.SelectedDateTime);
            input.Text = "2026-10-08 12:00:00";
            Assert.False(picker.HasInputError);
            Assert.Equal(original, picker.SelectedDateTime);
            picker.DisplayDateEnd = new DateTime(2026, 10, 7);
            Assert.True(picker.HasInputError);
            picker.DisplayDateEnd = null;
            Assert.False(picker.HasInputError);
            var range = new CalendarDateRange(new DateTime(2026, 10, 9));
            picker.BlackoutDates.Add(range);
            Assert.False(picker.HasInputError);
            range.Start = new DateTime(2026, 10, 8);
            Assert.True(picker.HasInputError);
            picker.BlackoutDates.Clear();
            Assert.False(picker.HasInputError);
            input.Text = "invalid";
            PressKey(input, window, Key.Escape);
            Assert.False(picker.HasInputError);
            Assert.Equal(original, picker.SelectedDateTime);
            input.Text = "";
            Assert.False(picker.HasInputError);
            Assert.Equal(0, errors);
        }));
    }

    /// <summary>
    /// 弹出层非法字段即时反映到外层，取消后恢复正式值的有效状态。
    /// </summary>
    [Fact]
    public void InputError_TracksPopupColumnsAndCancel()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            picker.SelectedDateTime = new DateTime(2026, 10, 6, 12, 0, 0);
            picker.IsDropDownOpen = true;
            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            hour.ApplyTemplate();
            var input = Part<TextBox>(hour, "PART_Input");
            input.Text = "99";
            Assert.True(picker.HasInputError);
            Assert.False(picker.Commit());
            input.Text = "13";
            Assert.False(picker.HasInputError);
            Assert.Equal(12, picker.SelectedDateTime!.Value.Hour);
            input.Text = "";
            Assert.True(picker.HasInputError);
            picker.Cancel();
            Assert.False(picker.HasInputError);
        }));
    }

    /// <summary>
    /// 日期投影更新时保留时间精度和 Kind，清空行为保持一致。
    /// </summary>
    [Fact]
    public void DateProjection_PreservesTimeKindAndFraction_AndClearsBothValues()
    {
        WpfTestThread.Run(() =>
        {
            var original = new DateTime(2026, 10, 6, 15, 36, 9, DateTimeKind.Utc).AddTicks(1234);
            var picker = new DateTimePicker { SelectedDateTime = original };
            Assert.Equal(original.Date, picker.SelectedDate);
            picker.SelectedDate = new DateTime(2026, 10, 8, 22, 1, 1);
            Assert.Equal(original.AddDays(2), picker.SelectedDateTime);
            Assert.Equal(DateTimeKind.Utc, picker.SelectedDateTime!.Value.Kind);
            Assert.Equal(picker.SelectedDateTime.Value.Date, picker.SelectedDate);
            picker.SelectedDate = null;
            Assert.Null(picker.SelectedDateTime);
            picker.SelectedDate = new DateTime(2026, 10, 9);
            Assert.Equal(new DateTime(2026, 10, 9), picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 空值打开仅初始化事务，取消后仍保持空值。
    /// </summary>
    [Fact]
    public void EmptyPopup_InitializesNowWithoutCommitting_AndCancelDiscardsIt()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker();
            var before = DateTime.Now;
            picker.IsDropDownOpen = true;
            Assert.InRange(picker.PendingDateTime!.Value, before, DateTime.Now);
            Assert.Null(picker.SelectedDateTime);
            Assert.Null(picker.SelectedDate);
            picker.Cancel();
            Assert.Null(picker.PendingDateTime);
            Assert.Null(picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 一次确认只发布一次正式值变化，并保留双向绑定。
    /// </summary>
    [Fact]
    public void Commit_PublishesOneCoherentEvent_AndPreservesBindings()
    {
        WpfTestThread.Run(() =>
        {
            var source = new DateTimePicker();
            var picker = new DateTimePicker();
            var binding = new Binding(nameof(DateTimePicker.SelectedDateTime)) { Source = source };
            BindingOperations.SetBinding(picker, DateTimePicker.SelectedDateTimeProperty, binding);
            int notifications = 0;
            picker.SelectedDateTimeChanged += (_, args) =>
            {
                notifications++;
                Assert.Equal(args.NewValue!.Value.Date, picker.SelectedDate);
                Assert.Equal(args.NewValue, picker.SelectedDateTime);
            };
            picker.IsDropDownOpen = true;
            var expected = picker.PendingDateTime;
            Assert.True(picker.Commit());
            Assert.Equal(expected, picker.SelectedDateTime);
            Assert.Equal(expected, source.SelectedDateTime);
            Assert.True(BindingOperations.IsDataBound(picker, DateTimePicker.SelectedDateTimeProperty));
            Assert.Equal(1, notifications);
            Assert.False(picker.IsDropDownOpen);
            Assert.Null(picker.PendingDateTime);
        });
    }

    /// <summary>
    /// 事务期间的外部业务更新成为新的编辑起点。
    /// </summary>
    [Fact]
    public void ExternalUpdateDuringTransaction_RebasesPendingValue()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker { IsDropDownOpen = true };
            var value = new DateTime(2024, 2, 29, 1, 2, 3, DateTimeKind.Utc);
            picker.SelectedDateTime = value;
            Assert.Equal(value, picker.PendingDateTime);
            picker.Cancel();
            Assert.Equal(value, picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 文本使用指定区域精确解析，错误输入不污染正式值。
    /// </summary>
    [Fact]
    public void Text_UsesLanguageAndCustomFormat_AndReportsInvalidInput()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker
            {
                Language = XmlLanguage.GetLanguage("fr-FR"),
                DateTimeFormat = "dd MMMM yyyy HH:mm:ss",
            };
            var value = new DateTime(2026, 2, 3, 14, 15, 16);
            string text = value.ToString(picker.DateTimeFormat, CultureInfo.GetCultureInfo("fr-FR"));
            picker.Text = text;
            Assert.Equal(value, picker.SelectedDateTime);
            int errors = 0;
            picker.DateValidationError += (_, args) =>
            {
                errors++;
                Assert.IsType<FormatException>(args.Exception);
            };
            picker.Text = "not a date";
            Assert.Equal(1, errors);
            Assert.Equal(value, picker.SelectedDateTime);
            Assert.Equal("not a date", picker.Text);
            picker.Text = string.Empty;
            Assert.Null(picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 未改变的短格式文本保留隐藏的秒、亚秒和 Kind。
    /// </summary>
    [Fact]
    public void UnchangedShortText_DoesNotTruncateHiddenPrecisionOrKind()
    {
        WpfTestThread.Run(() =>
        {
            var value = new DateTime(2026, 10, 6, 15, 36, 9, DateTimeKind.Utc).AddTicks(1234);
            var picker = new DateTimePicker { DateTimeFormat = "yyyy/MM/dd HH:mm", SelectedDateTime = value };
            Assert.True(picker.CommitText());
            Assert.Equal(value, picker.SelectedDateTime);
            Assert.Equal(DateTimeKind.Utc, picker.SelectedDateTime!.Value.Kind);
        });
    }

    /// <summary>
    /// 非法格式字符串在赋值前被拒绝。
    /// </summary>
    [Fact]
    public void InvalidFormat_IsRejectedBeforeChangingProperty()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker();
            Assert.Throws<ArgumentException>(() => picker.DateTimeFormat = "%");
            Assert.Equal("yyyy-MM-dd HH:mm:ss", picker.DateTimeFormat);
        });
    }

    /// <summary>
    /// 所有外部和文本入口都遵守日期范围与禁止日期。
    /// </summary>
    [Fact]
    public void RangeAndBlackouts_RejectExternalAndTextValues()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker
            {
                DisplayDateStart = new DateTime(2026, 10, 1),
                DisplayDateEnd = new DateTime(2026, 10, 31),
            };
            picker.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 10, 8)));
            Assert.Throws<ArgumentOutOfRangeException>(() => picker.SelectedDateTime = new DateTime(2026, 9, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => picker.SelectedDate = new DateTime(2026, 10, 8));
            Assert.Null(picker.SelectedDateTime);
            int errors = 0;
            picker.DateValidationError += (_, _) => errors++;
            picker.Text = "2026-10-08 13:00:00";
            Assert.Null(picker.SelectedDateTime);
            Assert.Equal(1, errors);
            picker.Text = "2026-10-31 23:59:59";
            Assert.Equal(new DateTime(2026, 10, 31, 23, 59, 59), picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 空值在范围外打开时不扩大日历范围，也不能直接确认。
    /// </summary>
    [Fact]
    public void OpeningOutsideDateRange_DoesNotExpandRange_AndCannotCommit()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker
            {
                DisplayDateStart = new DateTime(2000, 1, 1),
                DisplayDateEnd = new DateTime(2000, 12, 31),
                IsDropDownOpen = true,
            };
            Assert.False(picker.Commit());
            Assert.Null(picker.SelectedDateTime);
            Assert.Equal(new DateTime(2000, 12, 31), picker.DisplayDateEnd);
            Assert.False(DateTimePicker.SelectShortcutCommand.CanExecute("Now", picker));
        });
    }

    /// <summary>
    /// 快捷命令只同步弹出层预选值；取消保留原值，继续编辑后确认才提交。
    /// </summary>
    [Theory]
    [InlineData("Year", false)]
    [InlineData("Year", true)]
    [InlineData("Month", false)]
    [InlineData("Month", true)]
    [InlineData("Week", false)]
    [InlineData("Week", true)]
    [InlineData("Today", false)]
    [InlineData("Today", true)]
    [InlineData("Now", false)]
    [InlineData("Now", true)]
    public void Shortcuts_UpdatePendingValue_AndRequireConfirmation(string shortcut, bool hasValue)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            DateTime? original = hasValue ? new DateTime(2000, 6, 15, 12, 34, 56) : null;
            picker.SelectedDateTime = original;
            var originalText = picker.Text;
            var changes = 0;
            picker.SelectedDateTimeChanged += (_, _) => changes++;
            picker.FirstDayOfWeek = DayOfWeek.Monday;
            picker.IsDropDownOpen = true;
            var before = DateTime.Now;
            Assert.True(DateTimePicker.SelectShortcutCommand.CanExecute(shortcut, picker));
            DateTimePicker.SelectShortcutCommand.Execute(shortcut, picker);
            var actual = picker.PendingDateTime!.Value;
            Assert.Equal(DateTimeKind.Local, actual.Kind);
            Assert.True(picker.IsDropDownOpen);
            Assert.True(Part<Popup>(picker, "PART_Popup").IsOpen);
            Assert.Equal(original, picker.SelectedDateTime);
            Assert.Equal(original?.Date, picker.SelectedDate);
            Assert.Equal(originalText, picker.Text);
            Assert.Equal(0, changes);
            var expected = shortcut switch
            {
                "Year" => new DateTime(before.Year, 1, 1),
                "Month" => new DateTime(before.Year, before.Month, 1),
                "Week" => before.Date.AddDays(-((int)before.DayOfWeek + 6) % 7),
                "Today" => before.Date,
                _ => actual,
            };
            Assert.Equal(expected, actual);
            if (shortcut == "Now")
            {
                Assert.InRange(actual, before, DateTime.Now);
            }

            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            Assert.Equal(actual.Hour, hour.Value);
            Assert.Equal(actual.Minute, Part<DateTimePickerTimeColumn>(picker, "PART_Minute").Value);
            Assert.Equal(actual.Second, Part<DateTimePickerTimeColumn>(picker, "PART_Second").Value);
            var host = Part<ContentControl>(picker, "PART_CalendarHost");
            var calendar = Assert.IsType<Calendar>(host.Content);
            Assert.Equal(actual.Date, calendar.SelectedDate);
            Assert.Equal(actual.Date, calendar.DisplayDate);

            picker.Cancel();
            Assert.Null(picker.PendingDateTime);
            Assert.Equal(original, picker.SelectedDateTime);
            Assert.Equal(originalText, picker.Text);
            Assert.Equal(0, changes);

            picker.IsDropDownOpen = true;
            DateTimePicker.SelectShortcutCommand.Execute(shortcut, picker);
            var selectedHour = picker.PendingDateTime!.Value.Hour;
            DateTimePickerTimeColumn.StepCommand.Execute(1, hour);
            Assert.Equal((selectedHour + 1) % 24, picker.PendingDateTime!.Value.Hour);
            Assert.Equal(original, picker.SelectedDateTime);
            Assert.Equal(0, changes);
            var edited = picker.PendingDateTime;
            Assert.True(picker.Commit());
            Assert.Equal(edited, picker.SelectedDateTime);
            Assert.Equal(1, changes);
            Assert.False(picker.IsDropDownOpen);
        }));
    }

    /// <summary>
    /// 区域默认周起始日不会覆盖调用者的显式设置。
    /// </summary>
    [Fact]
    public void FirstDayOfWeek_DefaultFollowsLanguage_ExplicitValueWins()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker { Language = XmlLanguage.GetLanguage("fr-FR") };
            Assert.Equal(DayOfWeek.Monday, picker.FirstDayOfWeek);
            picker.FirstDayOfWeek = DayOfWeek.Saturday;
            picker.Language = XmlLanguage.GetLanguage("en-US");
            Assert.Equal(DayOfWeek.Saturday, picker.FirstDayOfWeek);
        });
    }

    /// <summary>
    /// 实际日历和时间列只编辑待提交值，取消后可重新恢复。
    /// </summary>
    [Fact]
    public void ActualTemplate_CalendarAndColumnsOnlyEditPending_AndCancelRestores()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            var original = new DateTime(2026, 10, 6, 23, 59, 59, DateTimeKind.Utc).AddTicks(7);
            picker.SelectedDateTime = original;
            picker.IsDropDownOpen = true;
            try
            {
                var host = Part<ContentControl>(picker, "PART_CalendarHost");
                var calendar = Assert.IsType<Calendar>(host.Content);
                calendar.SelectedDate = new DateTime(2026, 10, 8);
                Assert.True(picker.IsDropDownOpen);
                Assert.Equal(original, picker.SelectedDateTime);
                Assert.Equal(original.AddDays(2), picker.PendingDateTime);
                var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
                DateTimePickerTimeColumn.StepCommand.Execute(1, hour);
                Assert.Equal(0, picker.PendingDateTime!.Value.Hour);
                Assert.Equal(8, picker.PendingDateTime.Value.Day);
                Assert.Equal(59, picker.PendingDateTime.Value.Minute);
                Assert.Equal(DateTimeKind.Utc, picker.PendingDateTime.Value.Kind);
                Assert.Equal(7, picker.PendingDateTime.Value.Ticks % TimeSpan.TicksPerSecond);
                picker.Cancel();
                Assert.Equal(original, picker.SelectedDateTime);
                picker.IsDropDownOpen = true;
                Assert.Equal(original, picker.PendingDateTime);
                Assert.Equal(23, hour.Value);
            }
            finally
            {
                picker.Cancel();
            }
        });
    }

    /// <summary>
    /// 无效时间输入阻止确认，一位合法数字同步到循环列。
    /// </summary>
    [Fact]
    public void ColumnInput_InvalidStateBlocksCommit_AndSingleDigitSynchronizes()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            picker.SelectedDateTime = new DateTime(2026, 10, 6, 12, 30, 40);
            picker.IsDropDownOpen = true;
            try
            {
                var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
                hour.ApplyTemplate();
                var input = Part<TextBox>(hour, "PART_Input");
                input.Text = "2";
                Assert.Equal(2, hour.Value);
                Assert.True(picker.IsDropDownOpen);
                Assert.Equal(2, picker.PendingDateTime!.Value.Hour);
                Assert.Equal("2", input.Text);
                Assert.Equal("02", hour.VisibleValues[2]);
                input.Text = "24";
                Assert.False(hour.HasValidInput);
                Assert.Equal(2, picker.PendingDateTime.Value.Hour);
                Assert.False(picker.Commit());
                var calendar = (Calendar)Part<ContentControl>(picker, "PART_CalendarHost").Content;
                calendar.SelectedDate = new DateTime(2026, 10, 9);
                Assert.False(hour.HasValidInput);
                Assert.False(picker.Commit());
                input.Text = "23";
                Assert.True(picker.Commit());
                Assert.Equal(new DateTime(2026, 10, 9, 23, 30, 40), picker.SelectedDateTime);
            }
            finally
            {
                picker.Cancel();
            }
        });
    }

    /// <summary>
    /// 月份浏览保持待提交值，真实窗口关闭按取消处理。
    /// </summary>
    [Fact]
    public void BrowsingCalendar_DoesNotChangePending_AndPopupCloseCancels()
    {
        WpfTestThread.Run(async () =>
        {
            var picker = CreateTemplatedPicker();
            var window = new Window
            {
                Content = picker, Width = 600, Height = 100,
                ShowActivated = false, ShowInTaskbar = false,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                picker.IsDropDownOpen = true;
                var initial = picker.PendingDateTime;
                var calendar = (Calendar)Part<ContentControl>(picker, "PART_CalendarHost").Content;
                calendar.DisplayDate = new DateTime(2040, 5, 1);
                Assert.Equal(initial, picker.PendingDateTime);
                var popup = Part<Popup>(picker, "PART_Popup");
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                popup.Closed += (_, _) => closed.TrySetResult();
                popup.IsOpen = false;
                // Fade closes the native window asynchronously. Wait for its lifecycle event
                // while the test dispatcher continues processing animation and input messages.
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(picker.IsDropDownOpen);
                Assert.Null(picker.PendingDateTime);
                Assert.Null(picker.SelectedDateTime);
            }
            finally
            {
                picker.Cancel();
                window.Close();
            }
        });
    }

    /// <summary>
    /// 模板替换立即移除旧部件的内容和事件关系。
    /// </summary>
    [Fact]
    public void ReplacingTemplate_DetachesOldCalendarAndTimeFields()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            var oldHost = Part<ContentControl>(picker, "PART_CalendarHost");
            var oldColumn = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            picker.IsDropDownOpen = true;
            picker.Template = new ControlTemplate(typeof(DateTimePicker));
            picker.ApplyTemplate();
            Assert.Null(oldHost.Content);
            Assert.False(picker.IsDropDownOpen);
            picker.IsDropDownOpen = true;
            var pending = picker.PendingDateTime;
            oldColumn.Value = (oldColumn.Value + 1) % 24;
            Assert.Equal(pending, picker.PendingDateTime);
            picker.Cancel();
        });
    }

    /// <summary>
    /// 循环两端衔接且辅助技术只能设置范围内整数。
    /// </summary>
    [Fact]
    public void Column_WrapsBothDirections_AndHasAccessibleRange()
    {
        WpfTestThread.Run(() =>
        {
            var column = new DateTimePickerTimeColumn { Maximum = 23 };
            DateTimePickerTimeColumn.StepCommand.Execute(-1, column);
            Assert.Equal(23, column.Value);
            Assert.Equal(new[] { "21", "22", "23", "00", "01" }, column.VisibleValues);
            DateTimePickerTimeColumn.StepCommand.Execute(1, column);
            Assert.Equal(0, column.Value);
            var peer = UIElementAutomationPeer.CreatePeerForElement(column);
            var provider = Assert.IsAssignableFrom<IRangeValueProvider>(peer.GetPattern(PatternInterface.RangeValue));
            provider.SetValue(12);
            Assert.Equal(12, column.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetValue(24));
        });
    }

    /// <summary>
    /// 无需显式资源时，WPF 能从 Generic 找到发布的默认模板。
    /// </summary>
    [Fact]
    public void DefaultStyle_ResolvesFromGenericWithoutExplicitResources()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker();
            var window = new Window { Content = picker, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.NotNull(picker.Template);
                Assert.NotNull(Part<TextBox>(picker, "PART_TextBox"));
                Assert.NotNull(Part<Popup>(picker, "PART_Popup"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 时间列模板被移除后，旧输入框不再修改字段。
    /// </summary>
    [Fact]
    public void ReplacingColumnTemplate_DetachesOldInput()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            hour.ApplyTemplate();
            var input = Part<TextBox>(hour, "PART_Input");
            hour.Value = 12;
            input.Text = "invalid";
            Assert.False(hour.HasValidInput);
            hour.Template = new ControlTemplate(typeof(DateTimePickerTimeColumn));
            Assert.True(hour.HasValidInput);
            input.Text = "13";
            Assert.Equal(12, hour.Value);
        });
    }

    /// <summary>
    /// 辅助技术读取完整文本，并以折叠操作取消事务。
    /// </summary>
    [Fact]
    public void Automation_ExposesCompleteValue_AndCollapseCancels()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker();
            var peer = UIElementAutomationPeer.CreatePeerForElement(picker);
            var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
            value.SetValue("2026-10-06 15:36:09");
            Assert.Equal(new DateTime(2026, 10, 6, 15, 36, 9), picker.SelectedDateTime);
            var expand = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
            expand.Expand();
            Assert.True(picker.IsDropDownOpen);
            expand.Collapse();
            Assert.Null(picker.PendingDateTime);
            Assert.Equal("2026-10-06 15:36:09", value.Value);
        });
    }

    /// <summary>
    /// 主编辑框的 Enter、Esc 和 F4 遵循完整日期时间语义。
    /// </summary>
    [Fact]
    public void MainEditor_CommitsFullDateTimeOnEnter_AndEscRestoresInvalidText()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var input = Part<TextBox>(picker, "PART_TextBox");
            input.Text = "2024-02-29 23:59:58";
            Assert.Null(picker.SelectedDateTime);
            PressKey(input, window, Key.Enter);
            Assert.Equal(new DateTime(2024, 2, 29, 23, 59, 58), picker.SelectedDateTime);
            input.Text = "invalid";
            PressKey(input, window, Key.Escape);
            Assert.Equal("2024-02-29 23:59:58", input.Text);
            PressKey(input, window, Key.F4);
            Assert.True(picker.IsDropDownOpen);
            var popup = Part<Popup>(picker, "PART_Popup");
            PressKey(popup.Child, window, Key.Escape);
            Assert.False(picker.IsDropDownOpen);
        }));
    }

    /// <summary>
    /// 空时间输入阻止 Enter 提交，失焦后恢复最后合法值。
    /// </summary>
    [Fact]
    public void InvalidTime_EnterIsBlocked_LostFocusRestoresLastValidValue()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var original = new DateTime(2026, 10, 6, 12, 30, 40);
            picker.SelectedDateTime = original;
            picker.IsDropDownOpen = true;
            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            hour.ApplyTemplate();
            var input = Part<TextBox>(hour, "PART_Input");
            input.Text = string.Empty;
            PressKey(input, window, Key.Enter);
            Assert.True(picker.IsDropDownOpen);
            Assert.Equal(original, picker.SelectedDateTime);
            var focusArgs = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, picker)
            {
                RoutedEvent = Keyboard.LostKeyboardFocusEvent,
            };
            input.RaiseEvent(focusArgs);
            Assert.Equal("12", input.Text);
            Assert.True(hour.HasValidInput);
            input.Text = "13";
            PressKey(input, window, Key.Enter);
            Assert.False(picker.IsDropDownOpen);
            Assert.Equal(original.AddHours(1), picker.SelectedDateTime);
        }));
    }

    /// <summary>
    /// 组合输入和粘贴拒绝非数字，高精度滚轮累计到完整步长。
    /// </summary>
    [Fact]
    public void Column_RejectsNondigitCompositionAndPaste_AndAccumulatesWheelDelta()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            hour.ApplyTemplate();
            var input = Part<TextBox>(hour, "PART_Input");
            var composition = new TextComposition(InputManager.Current, input, "a");
            var textArgs = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
            {
                RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
            };
            input.RaiseEvent(textArgs);
            Assert.True(textArgs.Handled);
            var data = new DataObject(DataFormats.UnicodeText, "-1");
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
            input.RaiseEvent(paste);
            Assert.True(paste.CommandCancelled);
            var first = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 60) { RoutedEvent = Mouse.MouseWheelEvent };
            hour.RaiseEvent(first);
            Assert.Equal(0, hour.Value);
            var second = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 60) { RoutedEvent = Mouse.MouseWheelEvent };
            hour.RaiseEvent(second);
            Assert.Equal(23, hour.Value);
        });
    }

    /// <summary>
    /// 收紧范围不会被待提交日期反向扩大，非法事务不能提交。
    /// </summary>
    [Fact]
    public void PendingDateMadeInvalidByRangeChange_CannotBeCommitted()
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            picker.SelectedDateTime = new DateTime(2026, 10, 6);
            picker.IsDropDownOpen = true;
            try
            {
                var host = Part<ContentControl>(picker, "PART_CalendarHost");
                var calendar = (Calendar)host.Content;
                calendar.SelectedDate = new DateTime(2026, 10, 9);
                picker.DisplayDateEnd = new DateTime(2026, 10, 8);
                Assert.False(picker.Commit());
                Assert.Equal(new DateTime(2026, 10, 8), calendar.DisplayDateEnd);
                Assert.Equal(new DateTime(2026, 10, 6), picker.SelectedDateTime);
                Assert.Null(calendar.SelectedDate);
            }
            finally
            {
                picker.Cancel();
            }
        });
    }

    /// <summary>
    /// 禁用和卸载会取消事务，禁用期间不能打开弹出层。
    /// </summary>
    [Fact]
    public void DisabledAndUnloadedControls_CancelPendingTransactions()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker { IsDropDownOpen = true };
            picker.IsEnabled = false;
            Assert.False(picker.IsDropDownOpen);
            Assert.Null(picker.PendingDateTime);
            picker.IsDropDownOpen = true;
            Assert.False(picker.IsDropDownOpen);
            picker.IsEnabled = true;
            picker.IsDropDownOpen = true;
            picker.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(picker.IsDropDownOpen);
            Assert.Null(picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// DateTime 两端的时间字段循环不会进位或溢出。
    /// </summary>
    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0, -1, 23)]
    [InlineData(9999, 12, 31, 23, 59, 59, 1, 0)]
    public void FieldWrap_AtDateTimeExtremes_DoesNotOverflow(
        int year, int month, int day, int hourValue, int minute, int second, int step, int expectedHour)
    {
        WpfTestThread.Run(() =>
        {
            var picker = CreateTemplatedPicker();
            var original = new DateTime(year, month, day, hourValue, minute, second, DateTimeKind.Utc);
            picker.SelectedDateTime = original;
            picker.IsDropDownOpen = true;
            try
            {
                var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
                DateTimePickerTimeColumn.StepCommand.Execute(step, hour);
                Assert.Equal(original.Date, picker.PendingDateTime!.Value.Date);
                Assert.Equal(expectedHour, picker.PendingDateTime.Value.Hour);
                Assert.True(picker.Commit());
            }
            finally
            {
                picker.Cancel();
            }
        });
    }

    /// <summary>
    /// 浅色和深色主题下，发布模板同步时间值并将操作命令绑定到所属控件。
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void PublishedTemplate_BindsTimeValuesAndActionCommands(string theme)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var themeDictionary = new ResourceDictionary
            {
                Source = new Uri($"/Wpf.Zen.Ui;component/Resources/Theme/{theme}.xaml", UriKind.Relative),
            };
            picker.Resources.MergedDictionaries.Add(themeDictionary);
            picker.SelectedDateTime = new DateTime(2026, 10, 6, 15, 36, 9);
            picker.IsDropDownOpen = true;
            try
            {
                var popup = Part<Popup>(picker, "PART_Popup");
                var child = (FrameworkElement)popup.Child;
                // Command sources refresh CanExecute asynchronously after opening the popup.
                CommandManager.InvalidateRequerySuggested();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
                Assert.Equal("15", hour.VisibleValues[2]);
                var minute = Part<DateTimePickerTimeColumn>(picker, "PART_Minute");
                Assert.Equal("36", minute.VisibleValues[2]);
                var second = Part<DateTimePickerTimeColumn>(picker, "PART_Second");
                Assert.Equal("09", second.VisibleValues[2]);
                var actions = FindButtons(child);
                var cancel = Assert.Single(actions, button => button.Command == DateTimePicker.CancelCommand);
                var ok = Assert.Single(actions, button => button.Command == DateTimePicker.CommitCommand);
                Assert.Same(picker, cancel.CommandTarget);
                Assert.Same(picker, ok.CommandTarget);
                Assert.True(cancel.IsEnabled);
                Assert.True(ok.IsEnabled);

                foreach (var parameter in new[] { "Year", "Month", "Week", "Today", "Now" })
                {
                    var shortcut = Assert.Single(actions, button =>
                        button.Command == DateTimePicker.SelectShortcutCommand
                        && Equals(button.CommandParameter, parameter));
                    Assert.Same(picker, shortcut.CommandTarget);
                    Assert.True(shortcut.IsEnabled);
                }
            }
            finally
            {
                picker.Cancel();
            }
        }));
    }

    /// <summary>
    /// 遍历发布模板中的按钮，包括日历当前未显示的浏览模式，以便按命令定位操作。
    /// </summary>
    private static List<System.Windows.Controls.Button> FindButtons(DependencyObject parent)
    {
        var result = new List<System.Windows.Controls.Button>();
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is System.Windows.Controls.Button button)
            {
                result.Add(button);
            }

            result.AddRange(FindButtons(child));
        }

        return result;
    }

    /// <summary>
    /// 数字日期前缀按格式补齐末尾字段，且不继承原正式值的月日时间。
    /// </summary>
    [Theory]
    [InlineData("2026", "2026-01-01 00:00:00")]
    [InlineData("2026-1", "2026-01-01 00:00:00")]
    [InlineData("2026-1-2", "2026-01-02 00:00:00")]
    [InlineData("2026-1-2 3", "2026-01-02 03:00:00")]
    [InlineData("2026-1-2 3:4", "2026-01-02 03:04:00")]
    [InlineData("2026-1-2 3:4:5", "2026-01-02 03:04:05")]
    [InlineData(" 2024-2-29 ", "2024-02-29 00:00:00")]
    public void NumericPrefix_CompletesMissingFields(string input, string expected)
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker
            {
                SelectedDateTime = new DateTime(2030, 12, 15, 16, 17, 18, DateTimeKind.Utc),
            };
            picker.Text = input;
            Assert.Equal(expected, picker.Text);
            Assert.Equal(DateTimeKind.Utc, picker.SelectedDateTime!.Value.Kind);
        });
    }

    /// <summary>
    /// 辅助解析遵循格式中的字段顺序、区域分隔符与转义字面量。
    /// </summary>
    [Theory]
    [InlineData("yyyy/MM/dd HH:mm:ss", "de-DE", "2026.1", "2026-01-01 00:00:00")]
    [InlineData("dd/MM/yyyy HH:mm:ss", "en-GB", "2/1/2026 3", "2026-01-02 03:00:00")]
    [InlineData("yyyy'年'MM'月'dd'日' HH:mm:ss", "zh-CN", "2026年1", "2026-01-01 00:00:00")]
    [InlineData("yyyy\\/MM\\/dd HH:mm:ss", "de-DE", "2026/1", "2026-01-01 00:00:00")]
    public void NumericPrefix_UsesConfiguredFormat(string format, string language, string input, string expected)
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker { DateTimeFormat = format, Language = XmlLanguage.GetLanguage(language) };
            picker.Text = input;
            Assert.NotNull(picker.SelectedDateTime);
            string actual = picker.SelectedDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.Equal(expected, actual);
        });
    }

    /// <summary>
    /// 非法字段或无法确定字段边界的输入保留原文，不修改正式值。
    /// </summary>
    [Theory]
    [InlineData("2026-0")]
    [InlineData("2026-13")]
    [InlineData("2026-2-29")]
    [InlineData("2026-1-1 24")]
    [InlineData("2026-1-1 1:60")]
    [InlineData("32")]
    [InlineData("20261")]
    [InlineData("2026-")]
    [InlineData("2026--1")]
    [InlineData("2026-1 extra")]
    public void NumericPrefix_InvalidOrAmbiguousInputIsPreserved(string input)
    {
        WpfTestThread.Run(() =>
        {
            var original = new DateTime(2025, 5, 5, 5, 5, 5);
            var picker = new DateTimePicker { SelectedDateTime = original };
            picker.Text = input;
            Assert.Equal(original, picker.SelectedDateTime);
            Assert.Equal(input, picker.Text);
        });
    }

    /// <summary>
    /// 日期补全仍遵守禁止日期，日/月输入按显式格式补当前年份。
    /// </summary>
    [Fact]
    public void NumericPrefix_RespectsConstraintsAndDefaultsCurrentYear()
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker();
            picker.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 1, 1)));
            picker.Text = "2026-1";
            Assert.Null(picker.SelectedDateTime);
            Assert.Equal("2026-1", picker.Text);
            picker.DateTimeFormat = "dd/MM/yyyy HH:mm:ss";
            picker.Text = "1/2";
            Assert.Equal(new DateTime(DateTime.Now.Year, 2, 1), picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 分隔符与字段范围决定片段含义，日期和时间分别采用规定的缺省值。
    /// </summary>
    [Theory]
    [InlineData("05-12", 0, 5, 12, 0, 0, 0)]
    [InlineData("12:36", -1, 0, 0, 12, 36, 0)]
    [InlineData("12:36:59", -1, 0, 0, 12, 36, 59)]
    [InlineData("596", 596, 1, 1, 0, 0, 0)]
    [InlineData("202", 202, 1, 1, 0, 0, 0)]
    [InlineData("60", 60, 1, 1, 0, 0, 0)]
    [InlineData("05-12 12:36", 0, 5, 12, 12, 36, 0)]
    [InlineData("596-2-29", 596, 2, 29, 0, 0, 0)]
    public void Fragments_CompleteUnambiguousFields(string text, int year, int month, int day,
        int hour, int minute, int second)
    {
        WpfTestThread.Run(() =>
        {
            var today = DateTime.Today;
            if (year == -1)
            {
                month = today.Month;
                day = today.Day;
            }

            if (year <= 0)
            {
                year = today.Year;
            }

            var picker = new DateTimePicker { Text = text };
            Assert.Equal(new DateTime(year, month, day, hour, minute, second), picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 不把歧义数字、无效日期时间或未完成分隔符当作有效值。
    /// </summary>
    [Theory]
    [InlineData("12")]
    [InlineData("59")]
    [InlineData("0")]
    [InlineData("05-32")]
    [InlineData("24:36")]
    [InlineData("12:60")]
    [InlineData("12:")]
    [InlineData("05-")]
    public void AmbiguousFragments_PreserveText(string text)
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker { Text = text };
            Assert.Null(picker.SelectedDateTime);
            Assert.Equal(text, picker.Text);
        });
    }

    /// <summary>
    /// 标准格式先展开，区域分隔符和显式月日顺序均参与补全。
    /// </summary>
    [Theory]
    [InlineData("G", "en-GB", "12/05")]
    [InlineData("dd.MM.yyyy HH:mm:ss", "de-DE", "12.05")]
    [InlineData("yyyy年MM月dd日 HH:mm:ss", "zh-CN", "05月12日")]
    public void Fragments_RespectFormatAndCulture(string format, string culture, string text)
    {
        WpfTestThread.Run(() =>
        {
            var picker = new DateTimePicker
            {
                Language = XmlLanguage.GetLanguage(culture),
                DateTimeFormat = format,
                Text = text,
            };
            Assert.Equal(new DateTime(DateTime.Today.Year, 5, 12), picker.SelectedDateTime);
        });
    }

    /// <summary>
    /// 清除按钮可清除错误草稿及正式值，且按下两个编辑器按钮均立即聚焦文本框。
    /// </summary>
    [Fact]
    public void EditorButtons_FocusOnPress_AndClearResetsValueAndDraft()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            window.Activate();
            var input = Part<TextBox>(picker, "PART_TextBox");
            var clear = Part<ButtonBase>(picker, "PART_ClearButton");
            var open = Part<ButtonBase>(picker, "PART_Button");
            foreach (var button in new[] { clear, open })
            {
                window.Focus();
                var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                };
                button.RaiseEvent(mouse);
                Assert.True(input.IsKeyboardFocused);
                Assert.False(button.Focusable);
            }

            picker.SelectedDateTime = DateTime.Today;
            input.Text = "invalid";
            Assert.Equal(Visibility.Visible, clear.Visibility);
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Null(picker.SelectedDateTime);
            Assert.Null(picker.SelectedDate);
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
            input.Text = "invalid again";
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(string.Empty, input.Text);
            picker.IsClearButtonEnabled = false;
            picker.SelectedDateTime = DateTime.Today;
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(DateTime.Today, picker.SelectedDateTime);
        }));
    }

    /// <summary>
    /// 无效草稿允许通过按钮或 F4 打开选择器；取消保留草稿，确认相同值也覆盖错误文本。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidDraft_DoesNotBlockPopup_AndSameValueCommitReplacesText(bool useKeyboard)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var original = new DateTime(2026, 1, 1);
            picker.SelectedDateTime = original;
            var input = Part<TextBox>(picker, "PART_TextBox");
            var button = Part<System.Windows.Controls.Button>(picker, "PART_Button");
            input.Text = "invalid";
            picker.DateValidationError += (_, args) => args.ThrowException = true;
            if (useKeyboard)
            {
                PressKey(input, window, Key.F4);
            }
            else
            {
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }

            Assert.True(picker.IsDropDownOpen);
            Assert.Equal(original, picker.PendingDateTime);
            picker.Cancel();
            Assert.Equal("invalid", input.Text);
            PressKey(input, window, Key.F4);
            Assert.True(picker.Commit());
            Assert.Equal("2026-01-01 00:00:00", input.Text);
            Assert.Equal(original, picker.SelectedDateTime);
        }));
    }

    /// <summary>
    /// 程序聚焦与点击控件边框都转入文本框；打开按钮不参与键盘焦点。
    /// </summary>
    [Fact]
    public void FocusOnControlOrChrome_ForwardsToEditor_AndButtonIsNotFocusable()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            window.Activate();
            var input = Part<TextBox>(picker, "PART_TextBox");
            var button = Part<System.Windows.Controls.Button>(picker, "PART_Button");
            Assert.True(picker.Focusable);
            Assert.False(button.Focusable);
            Assert.False(button.IsTabStop);
            picker.Focus();
            Assert.True(input.IsKeyboardFocused);
            window.Focus();
            var chrome = Part<Border>(picker, "Chrome");
            var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            };
            chrome.RaiseEvent(mouse);
            Assert.True(input.IsKeyboardFocused);
            Assert.False(button.Focus());
            Assert.True(input.IsKeyboardFocused);
        }));
    }

    /// <summary>
    /// 编辑过程保留用户输入，只有 Enter 或失焦提交时才补全并格式化。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartialEditorText_CompletesOnlyOnSubmission(bool useEnter)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var input = Part<TextBox>(picker, "PART_TextBox");
            input.Text = "2026-1";
            Assert.Null(picker.SelectedDateTime);
            Assert.Equal("2026-1", input.Text);
            if (useEnter)
            {
                PressKey(input, window, Key.Enter);
            }
            else
            {
                var focusArgs = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, window)
                {
                    RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                };
                input.RaiseEvent(focusArgs);
            }

            Assert.Equal(new DateTime(2026, 1, 1), picker.SelectedDateTime);
            Assert.Equal("2026-01-01 00:00:00", input.Text);
        }));
    }

    /// <summary>
    /// 键盘及原生弹出窗口测试使用实际呈现源，并确保窗口和事务被清理。
    /// </summary>
    private static void WithWindow(Action<DateTimePicker, Window> action)
    {
        var picker = CreateTemplatedPicker();
        var window = new Window
        {
            Content = picker, Width = 600, Height = 100,
            ShowActivated = false, ShowInTaskbar = false,
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            action(picker, window);
        }
        finally
        {
            picker.Cancel();
            window.Close();
        }
    }

    /// <summary>
    /// 占位内容支持绑定，显示状态由草稿文本决定，并在更换模板后保持同步。
    /// </summary>
    [Fact]
    public void Placeholder_FollowsDraftAndSurvivesRetemplate()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            var source = new System.Windows.Controls.TextBlock { Text = "选择时间" };
            BindingOperations.SetBinding(picker, PlaceholderService.ContentProperty,
                new Binding(nameof(System.Windows.Controls.TextBlock.Text)) { Source = source });
            var placeholder = Part<PlaceholderControl>(picker, "PART_Placeholder");
            Assert.Equal("选择时间", placeholder.Content);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            Assert.False(placeholder.IsHitTestVisible);
            picker.Text = "invalid";
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            picker.Text = "";
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            source.Text = "新的提示";
            Assert.Equal("新的提示", placeholder.Content);
            var template = picker.Template;
            picker.Template = null;
            picker.Template = template;
            picker.ApplyTemplate();
            placeholder = Part<PlaceholderControl>(picker, "PART_Placeholder");
            Assert.Equal("新的提示", placeholder.Content);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            Assert.Null(picker.SelectedDateTime);
        }));
    }

    /// <summary>
    /// 语言变更同时更新按钮、弹出层字段和验证消息；未知翻译回退英文。
    /// </summary>
    [Theory]
    [InlineData("zh-CN", "选择日期和时间", "时", "无法根据")]
    [InlineData("en-US", "Choose date and time", "Hour", "The input")]
    [InlineData("fr-FR", "Choisir la date et l’heure", "Heure", "La saisie")]
    [InlineData("de-DE", "Datum und Uhrzeit auswählen", "Stunde", "Die Eingabe")]
    [InlineData("es-ES", "Seleccionar fecha y hora", "Hora", "No se puede")]
    [InlineData("it-IT", "Seleziona data e ora", "Ora", "Impossibile")]
    [InlineData("ja-JP", "日時を選択", "時", "DateTimeFormat")]
    [InlineData("ko-KR", "날짜 및 시간 선택", "시", "DateTimeFormat")]
    [InlineData("pl-PL", "Wybierz datę i godzinę", "Godzina", "Nie można")]
    [InlineData("pt-BR", "Selecionar data e hora", "Hora", "Não é possível")]
    [InlineData("ru-RU", "Выбрать дату и время", "Час", "Не удалось")]
    [InlineData("tr-TR", "Tarih ve saat seç", "Saat", "Girdi")]
    [InlineData("cs-CZ", "Vybrat datum a čas", "Hodina", "Vstup")]
    [InlineData("zh-TW", "選擇日期和時間", "時", "無法根據")]
    [InlineData("ar-SA", "Choose date and time", "Hour", "The input")]
    public void Localization_FollowsLanguage(string language, string choose, string hour, string error)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            picker.Language = XmlLanguage.GetLanguage("en-US");
            picker.Language = XmlLanguage.GetLanguage(language);
            window.UpdateLayout();
            var button = Part<ButtonBase>(picker, "PART_Button");
            Assert.Equal(choose, System.Windows.Automation.AutomationProperties.GetName(button));
            picker.IsDropDownOpen = true;
            window.UpdateLayout();
            var column = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            Assert.Equal(hour, System.Windows.Automation.AutomationProperties.GetName(column));
            picker.Cancel();
            string? message = null;
            picker.DateValidationError += (_, args) => message = args.Exception.Message;
            picker.Text = "invalid";
            Assert.False(picker.CommitText());
            Assert.StartsWith(error, message);
        }));
    }

    /// <summary>
    /// 每个卫星程序集必须包含完整翻译，防止缺失键被英文回退掩盖。
    /// </summary>
    [Fact]
    public void Localization_AllSatelliteResourcesContainEveryKey()
    {
        var manager = new System.Resources.ResourceManager(
            "Wpf.Zen.Ui.Controls.DateTimePicker.DateTimePickerStrings", typeof(DateTimePicker).Assembly);
        var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
        Assert.NotNull(neutral);
        string[] languages =
        {
            "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant",
        };
        foreach (string language in languages)
        {
            var culture = CultureInfo.GetCultureInfo(language);
            var translations = manager.GetResourceSet(culture, true, false);
            Assert.NotNull(translations);
            foreach (System.Collections.DictionaryEntry entry in neutral)
            {
                string key = (string)entry.Key;
                string? translation = translations.GetString(key);
                Assert.False(string.IsNullOrWhiteSpace(translation), language + ": " + key);
            }
        }
    }

    /// <summary>
    /// 日期按钮处理抬起事件后，鼠标捕获应回到 Popup，外围控件可以立即交互。
    /// </summary>
    [Fact]
    public void CalendarSelection_ReturnsCaptureToPopup()
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            window.Activate();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            picker.SelectedDateTime = new DateTime(2026, 10, 6, 15, 36, 9);
            picker.IsDropDownOpen = true;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var popup = Part<Popup>(picker, "PART_Popup");
            var root = (FrameworkElement)popup.Child;
            root.UpdateLayout();
            // 将弹出层移到当前指针下，避免模拟抬起时原生窗口按实际指针位置停用。
            var pointer = Mouse.GetPosition(root);
            popup.HorizontalOffset += pointer.X - root.ActualWidth / 2;
            popup.VerticalOffset += pointer.Y - root.ActualHeight / 2;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var popupCapture = Mouse.Captured;
            Assert.NotNull(popupCapture);
            var host = Part<ContentControl>(picker, "PART_CalendarHost");
            var calendar = Assert.IsType<Calendar>(host.Content);
            var item = Part<CalendarItem>(calendar, "PART_CalendarItem");
            var month = Part<Grid>(item, "PART_MonthView");
            CalendarDayButton? day = null;
            foreach (UIElement element in month.Children)
            {
                if (element is CalendarDayButton candidate
                    && candidate.DataContext is DateTime date && date == new DateTime(2026, 10, 7))
                {
                    day = candidate;
                    break;
                }
            }

            Assert.NotNull(day);
            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseDownEvent,
            };
            day.RaiseEvent(down);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(item, Mouse.Captured);
            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseUpEvent,
            };
            day.RaiseEvent(up);
            Assert.True(up.Handled);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(popupCapture, Mouse.Captured);
            Assert.True(popup.IsOpen);
            Assert.Equal(new DateTime(2026, 10, 7, 15, 36, 9), picker.PendingDateTime);
            Assert.Equal(new DateTime(2026, 10, 6, 15, 36, 9), picker.SelectedDateTime);

            var hour = Part<DateTimePickerTimeColumn>(picker, "PART_Hour");
            Assert.True(Mouse.Capture(hour, CaptureMode.SubTree));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            hour.ReleaseMouseCapture();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(popupCapture, Mouse.Captured);
            picker.Cancel();
            Assert.NotSame(item, Mouse.Captured);
        }));
    }

    /// <summary>
    /// 失焦丢弃非法草稿并恢复正式值；Enter 失败后继续失焦也应恢复，且不会重复校验。
    /// </summary>
    [Theory]
    [InlineData("invalid", false, false)]
    [InlineData("invalid", true, false)]
    [InlineData("invalid", true, true)]
    [InlineData("2026-09-30 12:00:00", true, false)]
    [InlineData("2026-10-08 12:00:00", true, false)]
    public void InvalidEditorText_LostFocusRestoresCommittedValue(
        string text, bool hasValue, bool submitFirst)
    {
        WpfTestThread.Run(() => WithWindow((picker, window) =>
        {
            DateTime? original = hasValue ? new DateTime(2026, 10, 6, 15, 36, 9) : null;
            picker.SelectedDateTime = original;
            picker.DisplayDateStart = new DateTime(2026, 10, 1);
            picker.DisplayDateEnd = new DateTime(2026, 10, 31);
            picker.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 10, 8)));
            var expectedText = picker.Text;
            var input = Part<TextBox>(picker, "PART_TextBox");
            int errors = 0;
            int valueChanges = 0;
            picker.DateValidationError += (_, _) => errors++;
            picker.SelectedDateTimeChanged += (_, _) => valueChanges++;
            input.Text = text;
            if (submitFirst)
            {
                PressKey(input, window, Key.Enter);
                Assert.Equal(text, input.Text);
                Assert.Equal(1, errors);
            }

            var focusArgs = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, window)
            {
                RoutedEvent = Keyboard.LostKeyboardFocusEvent,
            };
            input.RaiseEvent(focusArgs);

            Assert.Equal(original, picker.SelectedDateTime);
            Assert.Equal(original?.Date, picker.SelectedDate);
            Assert.Equal(expectedText, picker.Text);
            Assert.Equal(expectedText, input.Text);
            Assert.Equal(0, valueChanges);
            Assert.Equal(submitFirst ? 2 : 1, errors);

            input.RaiseEvent(focusArgs);
            Assert.Equal(submitFirst ? 2 : 1, errors);
        }));
    }

    private static void PressKey(UIElement target, Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
    }

    /// <summary>
    /// 加载发布用字典，确保测试覆盖实际 XAML 模板，而非测试专用替代模板。
    /// </summary>
    private static DateTimePicker CreateTemplatedPicker()
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri("/Wpf.Zen.Ui;component/Controls/DateTimePicker/DateTimePicker.xaml", UriKind.Relative),
        };
        var picker = new DateTimePicker { Style = (Style)dictionary[typeof(DateTimePicker)] };
        picker.Resources.MergedDictionaries.Add(dictionary);
        picker.ApplyTemplate();
        picker.Measure(new Size(600, 400));
        picker.Arrange(new Rect(0, 0, 600, 400));
        return picker;
    }

    private static T Part<T>(Control control, string name) where T : DependencyObject
    {
        object part = control.Template.FindName(name, control);
        return Assert.IsAssignableFrom<T>(part);
    }
}
