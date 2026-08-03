# Storyboard 复合属性路径动画风险清单

> 创建日期：2026-07-13
>
> 背景：WPF 的 `Storyboard.VerifyPathIsAnimatable` 要求复合属性路径（如 `(Border.BorderBrush).(SolidColorBrush.Opacity)`）中每一个中间对象必须为非 null 的 `DependencyObject`。当模板中的 `{TemplateBinding}`、资源标记扩展或样式 Setter 意外丢失时，中间对象为 null，动画系统抛出 `InvalidOperationException`。

## 已加固 ✅

| 控件 | 文件 | 动画目标路径 | 加固方式 |
|------|------|-------------|---------|
| `NavigationViewItem` | `NavigationLeftFluent.xaml:96,120` | `(Border.BorderBrush).(SolidColorBrush.Opacity)` | `CoerceBorderBrush` → 强制 null → `Transparent` |
| `controls:TreeViewItem` | `TreeViewItem.xaml:292,302` | `(Panel.Background).(SolidColorBrush.Opacity)` (无 TargetName) | `CoerceBackground` → 强制 null → `Transparent` |
| `TabItem` (系统控件) | `TabControl.xaml:62` | `(Panel.Background).(SolidColorBrush.Opacity)` | `CoerceTabItemBackground`（在 `TabControl` 静态构造函数中覆盖元数据） |

## 高风险但无自定义控件 ⚠️

无法通过 Coerce 加固，因为控件类型是 WPF 系统原生类型（无法在初始化时可靠覆盖元数据）。依赖样式 Setter 保证非 null。

| 控件 | 文件 | 行 | 依赖的 Setter | 风险场景 |
|------|------|-----|-------------|---------|
| `ButtonBase` (ToolBar) | `ToolBar.xaml` | 42, 54 | `ToolBarButtonBaseStyle` 第 15 行 `Background="{markup:BrushResource ControlFillColorPrimaryBrush}"` | 若任何人注释/删除该 Setter，动画崩溃 |

## 中风险

中间对象来自资源标记扩展，正常情况下非 null。若资源字典未加载或资源 Key 被删除，标记扩展可能返回 null。

| 控件 | 文件 | 行 | 依赖资源 |
|------|------|-----|---------|
| `RepeatButton` (DynamicScrollBar) | `DynamicScrollBar.xaml` | 51, 63 | `{markup:BrushResource SubtleFillColorTransparentBrush}` |
| `RepeatButton` (ScrollBar) | `ScrollBar.xaml` | 59, 71 | `{m:BrushResource SubtleFillColorTransparentBrush}` |
| `ListBoxItem` (AutoSuggestBox) | `AutoSuggestBox.xaml` | 69, 81 | 硬编码 `Background="Transparent"`，后由 Setter 改为资源画刷 |

## 低风险 ✓

中间对象（RenderTransform、ScaleTransform）在同一模板中显式创建为 `<RotateTransform>` / `<ScaleTransform>`，不会被外部覆盖。

| 控件 | 文件 |
|------|------|
| `CardExpander` | `CardExpander.xaml:67,78` |
| `NavigationViewItem` (Compact) | `NavigationViewCompact.xaml:160,177` |
| `DataGrid` (排序图标) | `DataGrid.xaml:679,691` |
| `ComboBox` / `NativeComboBox` | `ComboBox.xaml`, `NativeComboBox.xaml` |
| `ProgressRing` | `ProgressRing.xaml` |
| `ToggleSwitch` | `ToggleSwitch.xaml` |
| `Expander` | `Expander.xaml` |
| `NavigationView` (按钮) | `NavigationViewButtonStyles.xaml:95,107` — 单层路径 `(ScaleTransform.ScaleX)`，无中间对象 |

## 已排除（已注释或无害）

- **DataGrid.xaml:156, 247, 250, 268, 271, 475** — 全部在 `<!-- -->` 注释中
- **TreeViewItem.xaml:278**（CommonStates VisualStateGroup）— 已注释
- **NavigationViewTop.xaml**（Chevron 旋转动画）— 已注释
- **SplitButton.xaml** — `SolidColorBrush Opacity="..."` 只是画刷定义，不是动画
