# 主题色

WPF UI 获取一个基础主题色，并由此生成独立的浅色和深色主题调色板。控件使用面向 UI 的角色资源；原始的 Windows 颜色和 Windows 未公开的注册表调色板不会作为 UI 资源暴露。

## 配置主题色来源

默认来源为 `System`：

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemeManagerOptions
                AccentSource="System"
                ThemeMode="System" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

可选值如下：

- `System` 通过 Windows 公开的 `UISettings.GetColorValue(Accent)` API 读取个性化主题色。在 Windows 7 及该 API 不可用的其他系统上，WPF UI 将使用其默认蓝色。
- `Custom` 使用 `CustomAccentColor` 作为基础颜色。
- `Default` 始终使用 WPF UI 内置的 `#0078D4` 基础颜色。

在 XAML 中配置自定义颜色：

```xml
<ui:ThemeManagerOptions
    AccentSource="Custom"
    CustomAccentColor="#7A3FD0"
    ThemeMode="Dark" />
```

或在运行时配置：

```csharp
ThemeManager.Configure(
    themeMode: UiThemeMode.Dark,
    accentSource: AccentColorSource.Custom,
    customAccentColor: Color.FromRgb(0x7A, 0x3F, 0xD0)
);
```

`CustomAccentColor` 的 Alpha 通道将被忽略。不透明度是控件或其状态的属性，而非共享主题色调色板的属性。

## 主题色资源

每个角色都有对应的颜色（`ColorKeys` 和 `ColorId`）和画刷（`BrushKeys` 和 `BrushId`）：

- `AccentFillPrimary` 是较亮的填充层级。
- `AccentFillSecondary` 是常规的填充层级。
- `AccentFillTertiary` 是较暗的填充层级。
- `AccentTextPrimary` 是主要的主题色文本和图标层级。
- `AccentTextSecondary` 是较低强调级别的主题色文本和图标层级。
- `TextOnAccentPrimary` 为黑色或白色，选择依据是在所有三种主题色填充上均保持可读性。
- `AccentStroke` 用于主题色边框、焦点指示器等描边场景。
- `AccentSelection` 用于选中背景和选中指示器。

这些名称表达的是可复用的颜色层次结构，而非固定的控件状态。每个控件自行决定如何使用它们。例如，一个主题色按钮可以在正常状态使用 Secondary，悬停时使用 Primary，按下时使用 Tertiary：

```xml
<Style TargetType="ui:Button">
    <Setter Property="Background" Value="{ui:BrushResource AccentFillSecondaryBrush}" />
    <Setter Property="MouseOverBackground" Value="{ui:BrushResource AccentFillPrimaryBrush}" />
    <Setter Property="PressedBackground" Value="{ui:BrushResource AccentFillTertiaryBrush}" />
    <Setter Property="Foreground" Value="{ui:BrushResource TextOnAccentPrimaryBrush}" />
</Style>
```

对于禁用状态，请使用通用的主题资源。例如，使用 `ControlFillColorDisabledBrush` 和 `TextFillColorDisabledBrush`，而非派生另一种主题色。

## 调色板生成

Windows 通过公开 API 暴露基础主题色，但并未记录用于重建周边个性化颜色阶梯的稳定算法。因此，WPF UI 在 OKLCH 色彩空间中生成自己的确定性调色板：

- 在 sRGB 色域允许的范围内保持色相不变；
- 降低饱和度而非裁剪单个 RGB 通道；
- 为浅色和深色主题提供不同的可用填充色调；
- Primary 在感知上比 Secondary 更亮，Secondary 比 Tertiary 更亮；
- `AccentTextPrimary` 调整为与主题表面至少 4.5:1 的对比度；
- 选择一个 `TextOnAccentPrimary` 颜色，使其在每种主题色填充层级上均提供至少 4.5:1 的对比度。

这意味着 WPF UI 生成的角色在内部保持一致，但不保证与 Windows 私有的 Light1/Dark1 调色板值相匹配。

## 高对比度

高对比度模式会绕过生成的调色板和配置的主题色来源。主题色角色会直接映射到对应的 WPF `SystemColors` 资源，如 Highlight、HighlightText 和 HotTrack，从而确保用户的无障碍设置始终保持权威性。

## 运行时更新

当 `AccentSource` 为 `System` 时，Windows 偏好设置通知会使 `ThemeManager` 重新读取基础颜色并重建两个调色板。资源使用者应使用 `BrushResource`/`ColorResource` 或等效的 `DynamicResource` 引用，以便现有控件能够原地更新。

如果应用程序代码需要在刷新后的资源已应用后做出响应，可使用 `ThemeManager.Changed` 事件：

```csharp
ThemeManager.Changed += (_, args) =>
{
    Debug.WriteLine($"主题已更改为 {args.AppliedTheme}。");
};
```
