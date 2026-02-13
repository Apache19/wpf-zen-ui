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
    /// <summary>
    /// Gets singleton instance of <see cref="ApplicationThemeManager"/>.
    /// </summary>
    public static ApplicationThemeManager Instance { get; } = new();

    private readonly DebounceDispatcher _themeChangedDebouncer = new();

    /// <summary>
    /// Gets or sets a value indicating whether the application theme follows the system theme.
    /// When set to <see langword="true"/>, the application theme will automatically update to match the system theme.
    /// </summary>
    public bool FollowSystemTheme { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the application uses the system accent color.
    /// When set to <see langword="true"/>, the application will automatically apply the system accent color.
    /// 开启后，系统主题色将会替换主题色资源
    /// </summary>
    /// <remarks>
    /// 此设置在设计模式无效（设计模式不会监听系统事件，也不会读取系统主题文件）
    /// </remarks>
    public bool UseSystemAccentColor { get; set; }

    // 读取或设置，设置时会使 App 级主题生效
    public ThemeMode ThemeMode { get; set; }

    public event EventHandler<ThemeChangedEventArgs>? Changed;

    private ApplicationThemeManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 合并主题资源字典
        if (DesignerHelper.IsInDesignMode)
        {
            return;
        }

        var application = Application.Current;
        if (application == null)
        {
            // 如果 Application 尚未创建，通过 Dispatcher 调度延迟重试
            Dispatcher.CurrentDispatcher.BeginInvoke(Initialize, DispatcherPriority.ApplicationIdle);
            return;
        }

        SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;

        application.Exit += Application_Exit;
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (sender is Application application)
        {
            application.Exit -= Application_Exit;
        }

        SystemEvents.UserPreferenceChanged     -= SystemEvents_UserPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;

        _themeChangedDebouncer.Dispose();
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleThemeChangedDebounced();
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Debug.WriteLine($"SystemEvents_UserPreferenceChanged: {e.Category}");

        // SystemEvents 和 SystemParameters 的事件早就过时了，且触发过于粗糙，
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