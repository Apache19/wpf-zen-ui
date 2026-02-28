// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Appearance.Accent;
using Wpf.Ui.Theming;
using Wpf.Ui.Threading;

// 特意使主题管理器可以使用ui:前缀
namespace Wpf.Ui;

/*

   方案名称：基于附加属性继承与自刷新字典的主题引擎

   一、核心组件设计

   A. 主题管理器（单例与枚举）

   用于提供全局状态存储，但不强制用户直接与其交互。

   ThemeType（枚举）：Light、Dark、Auto、Custom。

   ThemeManager（单例）：
   负责监听系统注册表（当为 Auto 模式时）。
   提供 CurrentTheme 属性与 ThemeChanged 事件。

   B. 主题附加属性（ThemeAttacher / ThemeManager）

   这是整个引擎的“传感器”和“执行者”。

   ThemeProperty（附加属性）：
   设置为 Inherits（属性继承），确保子元素能够自动感知主题。

   核心逻辑（PropertyChangedCallback）：
   当附加属性发生变化，或元素 Loaded 时，通过 PresentationSource.FromVisual 向上查找视觉树的根节点（例如 HostContainer 或 Window）。
   将 ThemeResourceDictionary 自动注入到根节点的 Resources.MergedDictionaries 中。

   配置型附加属性：
   LightSource、DarkSource、CustomSource（类型为 Uri），允许用户指定自定义主题资源路径。

   C. 自刷新资源字典（ThemeResourceDictionary）

   继承自 ResourceDictionary，负责“按需加载”主题资源。

   内部机制：
   订阅全局 ThemeChanged 事件。
   内部维护一个私有的 MergedDictionaries，根据当前主题类型加载对应的 XAML 文件。
   支持优先级查找机制：用户自定义路径 > 库内置默认路径。

   二、关键工作流程

   1. 初始化与注入

   当用户在 WPF 的 UserControl 顶层设置 ui:Theme.Type="Dark"。
   ThemeAttacher 监测到元素加载后，定位到 WinForms 的 HostContainer。
   将 ThemeResourceDictionary 实例（单例方式）注入到 HostContainer.Resources 中。

   2. 动态切换

   当用户更改 Theme.Type，或全局单例触发主题变更。
   所有已注入的 ThemeResourceDictionary 实例收到通知，清理旧的 Source 并加载新的 Source。
   由于 HostContainer 层级的资源已更新，所有下级控件（包括 ContextMenu）通过 DynamicResource 自动刷新。

   三、设计优势总结

   环境兼容性：
   完全脱离 Application.Current，完美兼容 WinForms 和 Office 插件环境。

   穿透能力：
   通过 PresentationSource 注入根节点，解决弹出菜单、工具提示等视觉树断裂导致的主题丢失问题。

   可定制性：
   支持通过附加属性传入自定义 .xaml 路径，允许用户覆盖默认主题资源。

   代码侵入性：
   极低。用户只需在 XAML 根节点添加一行附加属性，无需编写 C# 初始化代码。

   性能与内存：
   资源字典在根节点级别共享，避免每个控件重复加载。
   支持弱引用机制，防止内存泄漏。

   四、实施路线图（Next Steps）

   第一阶段：
   实现 ThemeResourceDictionary。
   验证当其手动合并到 UserControl 时，能够根据单例自动切换 Source。

   第二阶段：
   编写 ThemeAttacher 附加属性。
   实现 Inherits 元数据，以及通过 PresentationSource 定位 Root 的逻辑。

   第三阶段：
   实现“路径覆盖”机制。
   让字典能够从附加属性中读取 CustomSource 并优先加载。

   第四阶段：
   处理多线程兼容性问题（针对不同 UI 线程的 Dispatcher 隔离）。

   这套架构的精髓在于把“主题”从 Application 级别剥离出来，变成一个依附于视觉根节点的可传播状态场，有点像把重力从地球中心挪到了每个 PresentationSource 上🌌。它既不污染宿主，又能保证穿透力。

   ==================================

   好，那我根据你的提示，重新整理了一下思路
   主题管理器仍然提供全局配置功能。
   主题管理器同时持有两套主题资源字典，一套深色系，一套浅色系。
   主题管理器支持配置两个色系的附加字典Uir（不是替换，是附加）。
   全局主题切换走Application，无Application时则全局切换不生效。

   附加属性仍然提供，但只提供明暗类型或跟随全局的参数。
   附加属性被挂在谁身上，则把主题管理器里持有的响应主题字典合并进去。判断是否是非Application环境，如果没有Application，则尝试往上找一级，看看上一级是不是根容器，是根容器则在其没有主题字典的情况下塞入主题。
   附加属性挂载的时候订阅主题管理器的通知，以便跟随变化。

   这样设计就解决了全局配置问题，也能使用附加属性强制给某个节点设定单独的主题，或者是顶层仿冒“全局”，而且也不会有很多重复字典出现，节省内存资源。
   但是有一个问题就是：Resource在合并字典前必须先BeginInit，合并完后EndInit，这样才能保证多次合并结果（合并主字典、附加字典、强调色字典等）正确，所以附加属性挂载订阅通知是必须的。

   实际上主题管理器只需要持有2个全局字典即可(App在xaml里合并，附加属性挂载时合并到目标)，因为合并到目标的始终只有1个，而合并过程是：各目标Resource.BeginInit()->清空全局资源字典->合并基础字典到全局字典->合并附加字典到全局字典->合并强调色字典到全局字典->

 */

/// <summary>
/// 负责管理应用程序主题状态，监听系统主题事件，同步系统主题，协调主题资源加载
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "ThemeManager has application lifetime; disposal is unnecessary.")]
public sealed class ThemeManager
{
    internal const string ThemeBaseUri = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    private static readonly Uri LightThemeUri        = new(ThemeBaseUri + "Light.xaml", UriKind.Absolute);
    private static readonly Uri DarkThemeUri         = new(ThemeBaseUri + "Dark.xaml", UriKind.Absolute);
    private static readonly Uri HighContrastThemeUri = new(ThemeBaseUri + "HighContrast.xaml", UriKind.Absolute);

    #region Attached Dependency Property BaseTheme

    /// <summary>
    /// Identifies the BaseTheme attached property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is used to set the theme for the RootVisual element.
    /// It functions as a "pseudo-global" theme but is scoped strictly to the current visual tree.
    /// It is specifically designed to resolve theme desynchronization issues for isolated components
    /// such as ContextMenus and Popups when WPF content is hosted in non-WPF environments (e.g., WinForms ElementHost).
    /// </para>
    /// <para>
    /// In non-native WPF hosting environments (e.g., WinForms ElementHost), 
    /// this property should be declared on the root node of the top-level UserControl.
    /// This ensures that theme resources are injected into the hidden root container, 
    /// allowing ContextMenus and Popups to synchronize correctly without a global Application object.
    /// </para>
    /// </remarks>
    public static readonly DependencyProperty BaseThemeProperty =
        DependencyProperty.RegisterAttached(
            "BaseTheme",
            typeof(UIThemeMode),
            typeof(ThemeManager),
            new PropertyMetadata(
                UIThemeMode.Auto,
                static (d, e) => OnElementThemePropertyChanged(d, e)
            )
        );

    /// <summary>
    /// Helper for getting <see cref="BaseThemeProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="BaseThemeProperty"/> from.
    /// </param>
    /// <returns>
    /// BaseTheme property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UIThemeMode GetBaseTheme(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (UIThemeMode)element.GetValue(BaseThemeProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="BaseThemeProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="BaseThemeProperty"/> on.
    /// </param>
    /// <param name="value">
    /// BaseTheme property value.
    /// </param>
    /// <remarks>
    /// <para>
    /// This property is used to set the theme for the RootVisual element.
    /// It functions as a "pseudo-global" theme but is scoped strictly to the current visual tree.
    /// It is specifically designed to resolve theme desynchronization issues for isolated components
    /// such as ContextMenus and Popups when WPF content is hosted in non-WPF environments (e.g., WinForms ElementHost).
    /// </para>
    /// <para>
    /// In non-native WPF hosting environments (e.g., WinForms ElementHost), 
    /// this property should be declared on the root node of the top-level UserControl.
    /// This ensures that theme resources are injected into the hidden root container, 
    /// allowing ContextMenus and Popups to synchronize correctly without a global Application object.
    /// </para>
    /// </remarks>
    public static void SetBaseTheme(DependencyObject element, UIThemeMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(BaseThemeProperty, value);
    }

    #endregion

    #region Attached Dependency Property RequestedTheme

    /// <summary>
    /// Identifies the RequestedTheme attached property.
    /// </summary>
    public static readonly DependencyProperty RequestedThemeProperty =
        DependencyProperty.RegisterAttached(
            "RequestedTheme",
            typeof(UIThemeMode),
            typeof(ThemeManager),
            new FrameworkPropertyMetadata(
                UIThemeMode.Auto,
                static (d, e) =>
                {
                    var ps = PresentationSource.FromDependencyObject(d);
                    if (ps != null)
                    {
                        OnElementThemePropertyChanged(d, e);
                    }
                }
            )
        );

    /// <summary>
    /// Helper for getting <see cref="RequestedThemeProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="RequestedThemeProperty"/> from.
    /// </param>
    /// <returns>
    /// RequestedTheme property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UIThemeMode GetRequestedTheme(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (UIThemeMode)element.GetValue(RequestedThemeProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="RequestedThemeProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="RequestedThemeProperty"/> on.
    /// </param>
    /// <param name="value">
    /// RequestedTheme property value.
    /// </param>
    public static void SetRequestedTheme(DependencyObject element, UIThemeMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(RequestedThemeProperty, value);
    }

    #endregion

    private static void OnElementThemePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 属性设置时把目标注册到弱引用列表里
        // 订阅其卸载事件，在事件中移除引用
        // 立即为目标挂载资源
        throw new NotImplementedException();
    }

    // 三个全局字典 Auto, Light, Dark
    // 当

    // todo 用空白项目测试ElementHost那个下级容器到底是什么

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
                //SystemAccentThemeManager.Disable(app);
                ApplyTheme(app, _themeMode, true, true);
            }
        }
        else
        {
            if (app != null)
            {
                ApplyTheme(app, _themeMode, false, true);
                //SystemAccentThemeManager.Update(app, UseSystemAccentColor, _themeMode);
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