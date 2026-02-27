// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance.Accent;
using Wpf.Ui.Theming;
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
    // todo 高对比度也有明暗模式。。。

    internal const string ThemeBaseUri = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    private static readonly Uri LightThemeUri        = new(ThemeBaseUri + "Light.xaml", UriKind.Absolute);
    private static readonly Uri DarkThemeUri         = new(ThemeBaseUri + "Dark.xaml", UriKind.Absolute);
    private static readonly Uri HighContrastThemeUri = new(ThemeBaseUri + "HighContrast.xaml", UriKind.Absolute);

    public static ThemeManager Instance { get; } = new();

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static readonly List<WeakReference> _registeredElements     = [];
    private static readonly object              _registeredElementsLock = new();

    private static bool        _followSystemTheme;
    private static UIThemeMode _themeMode = UIThemeMode.Light;

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

    public static UIThemeMode ThemeMode
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

    /// <summary>
    /// Registers a FrameworkElement to receive automatic theme updates.
    /// </summary>
    /// <param name="element">The element to register.</param>
    public static void Register(FrameworkElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        lock (_registeredElementsLock)
        {
            CleanupDeadReferences();

            if (!IsElementRegistered(element))
            {
                _registeredElements.Add(new WeakReference(element));
                element.Unloaded += OnElementUnloaded;

                ApplyThemeToElement(element, _themeMode, UseSystemAccentColor);
            }
        }
    }

    /// <summary>
    /// Registers a FrameworkContentElement to receive automatic theme updates.
    /// </summary>
    /// <param name="element">The element to register.</param>
    public static void Register(FrameworkContentElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        lock (_registeredElementsLock)
        {
            CleanupDeadReferences();

            if (!IsElementRegistered(element))
            {
                _registeredElements.Add(new WeakReference(element));
                element.Unloaded += OnElementUnloaded;

                ApplyThemeToContentElement(element, _themeMode, UseSystemAccentColor);
            }
        }
    }

    /// <summary>
    /// Unregisters a FrameworkElement from receiving automatic theme updates.
    /// </summary>
    /// <param name="element">The element to unregister.</param>
    public static void Unregister(FrameworkElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        lock (_registeredElementsLock)
        {
            _registeredElements.RemoveAll(wr => !wr.IsAlive || ReferenceEquals(wr.Target, element));
            element.Unloaded -= OnElementUnloaded;
        }
    }

    /// <summary>
    /// Unregisters a FrameworkContentElement from receiving automatic theme updates.
    /// </summary>
    /// <param name="element">The element to unregister.</param>
    public static void Unregister(FrameworkContentElement element)
    {
        if (element == null)
        {
            return;
        }

        lock (_registeredElementsLock)
        {
            _registeredElements.RemoveAll(wr => !wr.IsAlive || ReferenceEquals(wr.Target, element));
            element.Unloaded -= OnElementUnloaded;
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            Unregister(fe);
        }
        else if (sender is FrameworkContentElement fce)
        {
            Unregister(fce);
        }
    }

    private static bool IsElementRegistered(object element)
    {
        return _registeredElements.Any(wr => wr.IsAlive && ReferenceEquals(wr.Target, element));
    }

    private static void CleanupDeadReferences()
    {
        _registeredElements.RemoveAll(wr => !wr.IsAlive);
    }

    /// <summary>
    /// Applies theme to a specific element and its visual tree (useful for WinForms ElementHost scenarios).
    /// </summary>
    /// <param name="element">The root element to apply theme to.</param>
    /// <param name="themeMode">Theme mode to apply.</param>
    /// <param name="useSystemAccentColor">Whether to use system accent color.</param>
    private static void ApplyThemeToElement(FrameworkElement element, UIThemeMode themeMode, bool useSystemAccentColor = false)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.Dispatcher.Invoke(() =>
        {
            var isHighContrast = SystemParameters.HighContrast;
            var isDarkMode     = themeMode == UIThemeMode.Dark;

            Uri themeUri =
                isHighContrast ? HighContrastThemeUri :
                isDarkMode     ? DarkThemeUri :
                                 LightThemeUri;

            ResourceDictionary? existingThemeDict = null;
            foreach (var dict in element.Resources.MergedDictionaries)
            {
                if (dict.Source != null &&
                    dict.Source.ToString().StartsWith(ThemeBaseUri, StringComparison.OrdinalIgnoreCase))
                {
                    existingThemeDict = dict;
                    break;
                }
            }

            element.Resources.BeginInit();

            if (existingThemeDict != null)
            {
                element.Resources.MergedDictionaries.Remove(existingThemeDict);
            }

            var themeDict = new ResourceDictionary { Source = themeUri };
            element.Resources.MergedDictionaries.Insert(0, themeDict);

            if (useSystemAccentColor && !isHighContrast)
            {
                var palette = SystemAccentPaletteReader.Read();
                if (palette != null)
                {
                    var accentDict = new ResourceDictionary
                    {
                        [ColorKeys.SystemAccentColor]          = palette.Accent,
                        [ColorKeys.SystemAccentColorPrimary]   = themeMode == UIThemeMode.Light ? palette.Dark1 : palette.Light1,
                        [ColorKeys.SystemAccentColorSecondary] = themeMode == UIThemeMode.Light ? palette.Dark2 : palette.Light2,
                        [ColorKeys.SystemAccentColorTertiary]  = themeMode == UIThemeMode.Light ? palette.Dark3 : palette.Light3
                    };

                    element.Resources.MergedDictionaries.Add(accentDict);
                }
            }

            element.Resources.EndInit();
        });
    }

    private static void ApplyThemeToContentElement(FrameworkContentElement element, UIThemeMode themeMode, bool useSystemAccentColor)
    {
        var dispatcher = element.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            var isHighContrast = SystemParameters.HighContrast;
            var isDarkMode     = themeMode == UIThemeMode.Dark;

            Uri themeUri =
                isHighContrast ? HighContrastThemeUri :
                isDarkMode     ? DarkThemeUri :
                                 LightThemeUri;

            ResourceDictionary? existingThemeDict = null;
            foreach (var dict in element.Resources.MergedDictionaries)
            {
                if (dict.Source != null &&
                    dict.Source.ToString().StartsWith(ThemeBaseUri, StringComparison.OrdinalIgnoreCase))
                {
                    existingThemeDict = dict;
                    break;
                }
            }

            element.Resources.BeginInit();

            if (existingThemeDict != null)
            {
                element.Resources.MergedDictionaries.Remove(existingThemeDict);
            }

            var themeDict = new ResourceDictionary { Source = themeUri };
            element.Resources.MergedDictionaries.Insert(0, themeDict);

            if (useSystemAccentColor && !isHighContrast)
            {
                var palette = SystemAccentPaletteReader.Read();
                if (palette != null)
                {
                    var accentDict = new ResourceDictionary
                    {
                        [ColorKeys.SystemAccentColor]          = palette.Accent,
                        [ColorKeys.SystemAccentColorPrimary]   = themeMode == UIThemeMode.Light ? palette.Dark1 : palette.Light1,
                        [ColorKeys.SystemAccentColorSecondary] = themeMode == UIThemeMode.Light ? palette.Dark2 : palette.Light2,
                        [ColorKeys.SystemAccentColorTertiary]  = themeMode == UIThemeMode.Light ? palette.Dark3 : palette.Light3
                    };

                    element.Resources.MergedDictionaries.Add(accentDict);
                }
            }

            element.Resources.EndInit();
        });
    }

    private static void UpdateRegisteredElements()
    {
        lock (_registeredElementsLock)
        {
            CleanupDeadReferences();

            foreach (var weakRef in _registeredElements.ToList())
            {
                if (!weakRef.IsAlive)
                {
                    continue;
                }

                var target = weakRef.Target;
                if (target is FrameworkElement fe)
                {
                    ApplyThemeToElement(fe, _themeMode, UseSystemAccentColor);
                }
                else if (target is FrameworkContentElement fce)
                {
                    ApplyThemeToContentElement(fce, _themeMode, UseSystemAccentColor);
                }
            }
        }
    }

    static ThemeManager()
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
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
        // todo 换逻辑：先构建字典，然后把字典合并进Application和已注册的元素中

        // 没有 Application 或者已经Shutdown，则不做更新，只通知
        var app = Application.Current;

        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted)
        {
            app = null;
        }

        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => UpdateTheme(source)));
            return;
        }

        // 系统高对比度主题是系统易用性机制的一部分，它的优先级高于主题系统，
        // 当系统被设置为“高对比度”模式时，不管是否跟随系统主题，我们都必须遵循高对比度模式。
        // 另外，高对比度环境下没有单独的“强调色”，所以也要用高对比度颜色覆盖强调色。
        // 另，高对比度环境实际上仍然有明暗模式。。。

        if (source == ThemeChangeSource.System && FollowSystemTheme)
        {
            _themeMode = GetSystemThemeMode();
        }

        // 合并期间不立即刷新，否则可能导致强调色应用不完全
        app?.Resources.BeginInit();

        if (SystemParameters.HighContrast)
        {
            if (app != null)
            {
                // 高对比度没有强调色，所以要清理之前可能应用的强调色
                SystemAccentThemeManager.Disable(app);
                ApplyTheme(app, _themeMode, true, true);
            }
        }
        else
        {
            if (app != null)
            {
                ApplyTheme(app, _themeMode, false, true);
                SystemAccentThemeManager.Update(app, UseSystemAccentColor, _themeMode);
            }
        }

        // 结束合并，统一生效
        app?.Resources.EndInit();

        UpdateRegisteredElements();

        Instance.Changed?.Invoke(Instance, new ThemeChangedEventArgs(_themeMode, source));
    }

    private static void ApplyTheme(Application app, UIThemeMode mode, bool highContrast = false, bool forceReload = false)
    {
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
            highContrast             ? HighContrastThemeUri :
            mode == UIThemeMode.Dark ? DarkThemeUri :
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

    private static UIThemeMode GetSystemThemeMode()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            -1
        );

        return value switch
        {
            1 => UIThemeMode.Light,
            0 => UIThemeMode.Dark,
            _ => UIThemeMode.Light // 没有注册表键值对或者是错误值
        };
    }

    /* 实测手动创建的Application在非WPF项目时没有什么用处，切换主题合并字典不会生效
       有可能的话找出原因再启用。
    internal static Application EnsureApplication()
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

        return new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    }
    */

    private static void VerifyAccess()
    {
        Application.Current?.Dispatcher.VerifyAccess();
    }

    public event EventHandler<ThemeChangedEventArgs>? Changed;

    private ThemeManager() { }
}