// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Threading;
using Microsoft.Win32;
using Wpf.Ui.Designer;
using Wpf.Ui.Markup;
using Wpf.Ui.Threading;

namespace Wpf.Ui.Appearance;

/// <summary>
/// 负责管理应用程序主题状态，监听系统主题事件，同步系统主题，协调主题资源加载
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "ApplicationThemeManager has application lifetime; disposal is unnecessary.")]
public sealed class ApplicationThemeManager
{
    public static ApplicationThemeManager Instance { get; } = new();

    private readonly DebounceDispatcher _themeChangedDebouncer = new();

    private ThemeMode _themeMode = ThemeMode.Light;

    public bool FollowSystemTheme { get; set; }

    public bool UseSystemAccentColor { get; set; }

    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            _themeMode = value;
            UpdateTheme();
        }
    }

    public event EventHandler<ThemeChangedEventArgs>? Changed;

    private ApplicationThemeManager()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (DesignerHelper.IsInDesignMode)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleThemeChangedDebounced();
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Debug.WriteLine($"SystemEvents_UserPreferenceChanged: {e.Category}");

        // SystemEvents 和 SystemParameters 的事件过时了，且触发过于粗糙，
        // 可能需要寻找更精确的监听方式。
        // 考虑使用隐身窗口接收正确的消息并过滤，来实现更精细的监听器。

        if (e.Category is
            UserPreferenceCategory.General or // 明暗模式变化会触发此类别多次（）
            UserPreferenceCategory.VisualStyle or
            UserPreferenceCategory.Color)
        {
            ScheduleThemeChangedDebounced();
        }
    }

    private void ScheduleThemeChangedDebounced()
    {
        _themeChangedDebouncer.DebounceAsync(TimeSpan.FromMilliseconds(250), OnSystemThemeChanged);
    }

    private void OnSystemThemeChanged()
    {
        UpdateTheme();
    }

    private void UpdateTheme()
    {
        // 系统高对比度主题是系统易用性机制的一部分，它的优先级高于主题系统，我们不能自己搞一套“高对比度主题”出来，这样不仅增加了实现复杂度，还会造成可访问性的不一致性
        // 更新系统主题色。系统主题色资源独立于

        if (FollowSystemTheme)
        {
            // 1、读取系统主题名
            // 2、系统主题转换为AppTheme
            // 3、设置当前主题

            /*
             * 关于高亮主题：去主题目录里找出所有高亮主题（有些是定制的）
             * 再展示，不要使用固定资源
             * 如果没有找到任何高亮主题资源，则使用默认高亮主题
             */
        }

        if (UseSystemAccentColor)
        {
            // 用强调色管理器替换强调色
        }
        else
        {
            // 合并强调色资源
        }
    }
}