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

    public bool UseSystemAccentColor { get; set; } // 只对普通主题生效，高对比度模式会被覆盖

    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            _themeMode = value;
            UpdateTheme();
        }
    }

    public event EventHandler? Changed;

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
        // todo 考虑是否要等待
        //   返回的任务是调度进去的任务，上一次调度的会被立即取消
        _ = _themeChangedDebouncer.DebounceAsync(
            TimeSpan.FromMilliseconds(250),
            OnSystemThemeChanged
        );
    }

    private void OnSystemThemeChanged()
    {
        UpdateTheme();
    }

    private void UpdateTheme()
    {
        // gptc 考虑到UpdateTheme可能被手动设置ThemeMode触发或是系统触发，系统触发时也要更新ThemeMode状态，所以如何解决这个冲突？
        // 		这是一个典型的状态同步问题。需要区分"用户手动设置"和"系统自动更新"两种场景。以下是解决方案：
        // 		
        // 		## 核心思路
        // 		添加一个标志位来区分触发源，避免循环更新。
        // 		
        // 		## 具体实现
        // 		
        // 		### 1. 添加触发源枚举
        // 		```csharp
        // 		private enum ThemeChangeSource
        // 		{
        // 		    Manual,     // 用户手动设置
        // 		    System,     // 系统触发
        // 		    Initial     // 初始化
        // 		}
        // 		```
        // 		
        // 		### 2. 修改 UpdateTheme 方法
        // 		```csharp
        // 		private void UpdateTheme(ThemeChangeSource source = ThemeChangeSource.Manual)
        // 		{
        // 		    // 如果是系统触发，先更新内部状态
        // 		    if (source == ThemeChangeSource.System && FollowSystemTheme)
        // 		    {
        // 		        _themeMode = GetSystemThemeMode();
        // 		    }
        // 		    
        // 		    // 应用主题的逻辑保持不变...
        // 		    ApplyTheme(_themeMode);
        // 		    
        // 		    // 其他逻辑...
        // 		}
        // 		```
        // 		
        // 		### 3. 修改属性设置器
        // 		```csharp
        // 		public ThemeMode ThemeMode
        // 		{
        // 		    get => _themeMode;
        // 		    set
        // 		    {
        // 		        if (_themeMode == value) return;
        // 		        
        // 		        _themeMode = value;
        // 		        UpdateTheme(ThemeChangeSource.Manual);
        // 		    }
        // 		}
        // 		```
        // 		
        // 		### 4. 修改系统触发处理
        // 		```csharp
        // 		private void OnSystemThemeChanged()
        // 		{
        // 		    if (!FollowSystemTheme) return; // 如果不跟随系统，忽略系统变化
        // 		    
        // 		    UpdateTheme(ThemeChangeSource.System);
        // 		}
        // 		```
        // 		
        // 		## 关键设计点
        // 		
        // 		1. **状态一致性**：系统触发时，自动更新 `_themeMode` 以保持状态一致
        // 		2. **避免循环**：手动设置时不会触发系统监听器的响应
        // 		3. **条件检查**：系统触发前检查 `FollowSystemTheme` 标志
        // 		4. **事件通知**：无论哪种触发源，最终都触发 `Changed` 事件
        // 		
        // 		## 额外建议
        // 		
        // 		考虑添加一个只读属性来反映实际应用的主题：
        // 		```csharp
        // 		public ThemeMode AppliedTheme => _themeMode;
        // 		```
        // 		
        // 		这样外部可以知道当前实际生效的主题，而不是用户设置的主题（当 `FollowSystemTheme=true` 时两者可能不同）。
        // gptc 


        // 系统高对比度主题是系统易用性机制的一部分，它的优先级高于主题系统，
        // 我们不能自己搞一套“高对比度主题”出来，这样不仅增加了实现复杂度，
        // 还会造成可访问性的不一致性。
        // 另外，高对比度环境下也没有“强调色”，所以，也要把高对比度颜色应用到强调色资源键上。
        if (SystemParameters.HighContrast)
        {
            ApplySystemHighContrastColors();
            return;
        }

        // 更新系统主题色。系统主题色资源独立于

        // todo 要考虑手动设置或是系统设置
        if (FollowSystemTheme)
        {
            // 1、读取系统主题名
            // 2、系统主题转换为AppTheme
            // 3、设置当前主题

            var theme = GetSystemTehmeMode();
            ApplyTheme(theme);
        }
        else
        {
            var theme = ThemeMode;
            ApplyTheme(theme);
        }

        if (UseSystemAccentColor)
        {
            ApplySystemAccent();
        }
        else
        {
            ApplyDefaultAccent();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySystemHighContrastColors()
    {
        // 实现系统高对比度颜色方案的逻辑
        // 并且不管是否开启 UseSystemAccentColor，都要把高对比度颜色应用到
    }

    private void ApplyTheme(ThemeMode mode)
    {
        // 应用指定主题
    }

    private void ApplySystemAccent()
    {
        // 1、清理强调色资源
        // 2、从系统获取强调色指，更新资源
    }

    private void ApplyDefaultAccent()
    {
        // 1、清理强调色资源
        // 2、合并默认强调色资源词典
    }

    private ThemeMode GetSystemTehmeMode()
    {
        // 1、获取系统主题模式（明暗）
        // 2、返回

        // 临时返回明亮主题
        return ThemeMode.Light;
    }
}