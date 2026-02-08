// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Microsoft.Win32;

namespace Wpf.Ui.Appearance;

// todo 改为库内部监听，增加System主题
public static class SystemThemeWatcher
{
    #region 防抖机制字段和常量

    private const int DebounceDelayMs = 300;

    #if NET9_0_OR_GREATER
    private static readonly Lock DebounceLock = new();
    #else
    private static readonly object DebounceLock = new();
    #endif

    private static Timer? _debounceTimer;

    #endregion

    private static volatile bool _isWatching;
    private static volatile bool _updateAccents;

    public static void Watch(bool updateAccents = true)
    {
        if (!_isWatching)
        {
            _updateAccents = updateAccents;

            SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;

            _isWatching = true;

            OnSystemThemeChanged();
        }
    }

    public static void UnWatch()
    {
        if (_isWatching)
        {
            _isWatching = false;

            lock (DebounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            SystemEvents.UserPreferenceChanged     -= SystemEvents_UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        }
    }

    private static void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleThemeChangedDebounced();
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Debug.WriteLine($"SystemEvents_UserPreferenceChanged: {e.Category}");

        // todo 寻找更精确的监听方式

        if (e.Category is
            UserPreferenceCategory.General or // 明暗模式变化触发此类别多次
            UserPreferenceCategory.VisualStyle or
            UserPreferenceCategory.Color)
        {
            ScheduleThemeChangedDebounced();
        }
    }

    private static void ScheduleThemeChangedDebounced()
    {
        lock (DebounceLock)
        {
            if (_debounceTimer is null)
            {
                _debounceTimer = new Timer(_ => DebounceTimerCallback(), null, DebounceDelayMs, Timeout.Infinite);
                return;
            }

            // 重置计时器
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
    }

    private static void DebounceTimerCallback()
    {
        lock (DebounceLock)
        {
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        try
        {
            // 确保主题应用在UI线程上运行
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null)
            {
                dispatcher.BeginInvoke(OnSystemThemeChanged);
            }
            else
            {
                OnSystemThemeChanged();
            }
        }
        catch
        {
            // 吞掉来自调度器调用的任何异常
        }
    }

    private static void OnSystemThemeChanged()
    {
        ApplicationThemeManager.ApplySystemTheme(_updateAccents);
    }
}