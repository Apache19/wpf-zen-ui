// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance.Accent;
using Wpf.Ui.Designer;
using Wpf.Ui.Threading;

namespace Wpf.Ui.Appearance;

/// <summary>
/// 负责管理应用程序主题状态，监听系统主题事件，同步系统主题，协调主题资源加载
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "ThemeManager has application lifetime; disposal is unnecessary.")]
public sealed class ThemeManager
{
    // todo 实现一个Update方法，传入FollowSystemTheme、UseSystemAccentColor、ThemeMode三个参数，能同时设置三个属性，并只执行一次更新

    internal const string ThemeBaseUri = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    private static readonly Uri LightThemeUri        = new(ThemeBaseUri + "Light.xaml", UriKind.Absolute);
    private static readonly Uri DarkThemeUri         = new(ThemeBaseUri + "Dark.xaml", UriKind.Absolute);
    private static readonly Uri HighContrastThemeUri = new(ThemeBaseUri + "HighContrast.xaml", UriKind.Absolute);

    public static ThemeManager Instance { get; } = new();

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static bool      _followSystemTheme;
    private static ThemeMode _themeMode = ThemeMode.Light;

    public static bool FollowSystemTheme
    {
        get => _followSystemTheme;
        set
        {
            if (_followSystemTheme != value)
            {
                _followSystemTheme = value;
                UpdateTheme(value ? ThemeChangeSource.System : ThemeChangeSource.Manual);
            }
        }
    }

    public static bool UseSystemAccentColor
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateTheme(ThemeChangeSource.Manual);
            }
        }
    }

    public static ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            // 有手动设置的意图，则将跟随系统置为否
            _followSystemTheme = false;

            if (_themeMode != value)
            {
                _themeMode = value;
                UpdateTheme(ThemeChangeSource.Manual);
            }
        }
    }

    static ThemeManager()
    {
        if (DesignerHelper.IsInDesignMode)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
    }

    private static void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Debug.WriteLine($"ThemeManager: SystemParameters_StaticPropertyChanged - {e.PropertyName}");
        ScheduleThemeChangedDebounced();
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Debug.WriteLine($"ThemeManager: SystemEvents_UserPreferenceChanged: {e.Category}");

        // SystemEvents 和 SystemParameters 的事件过时了，且触发过于粗糙，
        // 可能需要寻找更精确的监听方式。
        // 考虑使用隐身窗口接收正确的消息并过滤，来实现更精细的监听器。

        if (e.Category is
            UserPreferenceCategory.General or // 明暗模式变化会触发此类别多次
            UserPreferenceCategory.VisualStyle or
            UserPreferenceCategory.Color)
        {
            ScheduleThemeChangedDebounced();
        }
    }

    private static void ScheduleThemeChangedDebounced()
    {
        _ = _themeChangedDebouncer.DebounceAsync(
            TimeSpan.FromMilliseconds(250),
            OnSystemThemeChanged
        );
    }

    private static void OnSystemThemeChanged()
    {
        Application.Current?.Dispatcher.BeginInvoke(
            () => UpdateTheme(ThemeChangeSource.System)
        );
    }

    private static void UpdateTheme(ThemeChangeSource source)
    {
        VerifyAccess();

        // 系统高对比度主题是系统易用性机制的一部分，它的优先级高于主题系统，
        // 当系统被设置为“高对比度”模式时，我们必须遵循这一模式，
        // 而不是自己搞一套“高对比度主题”出来，这样不仅增加了实现复杂度，
        // 还会造成可访问性的不一致。
        // 另外，高对比度环境下没有单独的“强调色”，所以也要用高对比度颜色覆盖强调色。

        var app = EnsureApplication();
        app.Resources.BeginInit();

        if (SystemParameters.HighContrast)
        {
            // 高对比度没有强调色，所以要清理之前可能应用的强调色
            SystemAccentThemeManager.Disable();
            ApplySystemHighContrastColors();
        }
        else
        {
            if (source == ThemeChangeSource.System && FollowSystemTheme)
            {
                _themeMode = GetSystemThemeMode();
            }

            ApplyTheme(_themeMode, false, true);

            SystemAccentThemeManager.Update(UseSystemAccentColor, _themeMode);
        }

        app.Resources.EndInit();

        Instance.Changed?.Invoke(Instance, new ThemeChangedEventArgs(_themeMode, source));
    }

    private static void ApplySystemHighContrastColors()
    {
        ApplyTheme(_themeMode, true, true);
    }

    private static void ApplyTheme(ThemeMode mode, bool highContrast = false, bool forceReload = false)
    {
        var app    = EnsureApplication();
        var merged = app.Resources.MergedDictionaries;

        // 1️ 找到当前已加载的库主题字典（如果存在）
        ResourceDictionary? existingThemeDict = null;

        foreach (var dict in merged)
        {
            if (dict.Source == null)
            {
                continue;
            }

            var src = dict.Source.ToString();

            if (src.StartsWith(ThemeBaseUri, StringComparison.OrdinalIgnoreCase))
            {
                existingThemeDict = dict;
                break;
            }
        }

        // 2️ 根据 ThemeMode 决定目标主题 URI
        Uri targetUri =
            highContrast           ? HighContrastThemeUri :
            mode == ThemeMode.Dark ? DarkThemeUri :
                                     LightThemeUri;

        // 如果非强制且当前已经是目标主题，就不重复加载（幂等）
        if (!forceReload && existingThemeDict?.Source == targetUri)
        {
            return;
        }

        // 3️ 移除旧主题
        if (existingThemeDict != null)
        {
            merged.Remove(existingThemeDict);
        }

        // 4️ 加载新主题
        var newThemeDict = new ResourceDictionary { Source = targetUri };

        merged.Add(newThemeDict);
    }

    private static ThemeMode GetSystemThemeMode()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            -1
        );

        return value switch
        {
            1 => ThemeMode.Light,
            0 => ThemeMode.Dark,
            _ => ThemeMode.Light // 没有注册表键值对或者是错误值
        };
    }

    private static Application EnsureApplication()
    {
        // When WPFUI is used from non-WPF hosts (such as WinForms apps or plugins),
        // a WPF Application instance may not exist.
        // Theme switching requires a WPF Application to host resource dictionaries,
        // so we create one if necessary.

        if (Application.Current != null)
        {
            return Application.Current;
        }

        // Ensure we are on an STA thread before creating Application
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "A WPF Application must be created on an STA thread."
            );
        }

        return new Application();
    }

    private static void VerifyAccess()
    {
        Application.Current?.Dispatcher.VerifyAccess();
    }

    public event EventHandler<ThemeChangedEventArgs>? Changed;

    private ThemeManager() { }
}