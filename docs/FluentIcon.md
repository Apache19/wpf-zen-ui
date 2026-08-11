# FluentIcon 与图标呈现

WPF Zen UI 内置 Fluent System Icons Regular 和 Filled 字体，并通过
`FluentIcon`、`FluentIconSource` 与 `FluentIconSpan` 分别覆盖独立视觉元素、控件图标槽和
行内文本三种使用场景。

本文中的示例假定已声明 WPF Zen UI 的 XAML 命名空间：

```xml
xmlns:ui="http://schemas.thezendev.com/wpf/2027"
```

## 选择合适的图标形式

| 场景 | 推荐类型 | 原因 |
| --- | --- | --- |
| 在布局中直接显示图标 | `FluentIcon` | 是可绑定、可定位和可独立设置外观的视觉元素 |
| 为 `Button.Icon` 等图标槽提供内容 | `FluentIconSource` | 是可共享的数据，不会产生 Visual 重复父级问题 |
| 在 `TextBlock` 文本中插入图标 | `FluentIconSpan` | 作为 `Run` 参与文本排版、换行和基线对齐 |

不要把同一个 `FluentIcon` 实例放进资源后交给多个控件使用。WPF 的视觉元素只能拥有一个
父级；需要共享图标定义时，应使用 `FluentIconSource`。

## 直接显示 FluentIcon

`Symbol` 从 `SymbolRegular` 枚举中选择图标。枚举名称末尾的 `16`、`20`、`24` 或 `48`
表示该字形的设计网格，不会自动设置元素的显示尺寸；实际尺寸仍由 `IconSize` 或
`FontSize` 决定。

```xml
<StackPanel Orientation="Horizontal">
    <ui:FluentIcon
        Symbol="Home24"
        IconSize="20"
        Foreground="CornflowerBlue" />
    <ui:FluentIcon
        Margin="8,0,0,0"
        Symbol="Settings24"
        Filled="True"
        IconSize="20" />
</StackPanel>
```

`FluentIcon` 继承自 WPF `TextBlock`，因此仍可使用 `Foreground`、`FontSize`、
`FontWeight`、`Opacity`、`Margin`、布局属性和数据绑定。控件会自行选择内置图标字体并更新
字形，不应手动设置 `Text` 或 `FontFamily`。

在代码中也可以直接创建图标：

```csharp
var icon = new FluentIcon(SymbolRegular.Home24, filled: false)
{
    IconSize = 20,
};
```

## 为控件的 Icon 属性提供图标

库内控件的图标槽保留普通 WPF 内容语义，可以接收任意视觉元素或数据。对于 Fluent
图标，优先使用 `FluentIconSource` 标记扩展：

```xml
<ui:Button
    Content="Home"
    Icon="{ui:FluentIconSource Home24}" />

<ui:Button
    Content="Favorite"
    Icon="{ui:FluentIconSource Star24, Filled=True}" />
```

该扩展返回不可变的 `FluentIconSource` 数据，而不是 `FluentIcon` 视觉元素。库提供的隐式
`DataTemplate` 会在每次呈现时创建新的 `FluentIcon`，因此同一个源可以安全地出现在样式、
资源和多个控件中：

```xml
<Style x:Key="HomeButtonStyle" TargetType="{x:Type ui:Button}">
    <Setter Property="Icon" Value="{ui:FluentIconSource Home24}" />
</Style>
```

代码或视图模型可以保存同样的数据对象：

```csharp
public FluentIconSource HomeIcon { get; } =
    new(SymbolRegular.Home24);
```

`FluentIconSource.Filled` 只描述图标源自身偏好的默认形态。控件模板仍可通过
`IconPresenter.Variant` 在悬停、选中等状态下临时请求另一种形态，而不会修改源对象。

## 用图标呈现作用域隔离文本外观

WPF 的 `FontSize` 和 `FontWeight` 会沿元素树继承。在同时包含文本和图标的复杂布局中，
直接设置这些属性会同时改变两者。`IconPresenter.IconSize` 和
`IconPresenter.IconWeight` 提供了只由兼容图标消费的可继承呈现上下文：

```xml
<StackPanel
    FontSize="14"
    FontWeight="Regular"
    ui:IconPresenter.IconSize="12"
    ui:IconPresenter.IconWeight="SemiBold">

    <TextBlock Text="正文仍然是 14 / Regular" />
    <ui:FluentIcon Symbol="ChevronRight24" />
</StackPanel>
```

作用域属性会穿过 `ControlTemplate` 和 `DataTemplate` 的继承边界，因此调用方无需复制整个
控件模板。例如，可以只调整 `BreadcrumbBar` 模板内的分隔图标：

```xml
<ui:BreadcrumbBar
    ui:IconPresenter.IconSize="12"
    ui:IconPresenter.IconWeight="SemiBold"
    ItemsSource="{Binding Folders}" />
```

作用域可以设置在窗口、控件、面板、`IconPresenter` 或单个 `FluentIcon` 上。更接近图标的
值遵循 WPF 属性继承规则并覆盖外层作用域：

```xml
<StackPanel ui:IconPresenter.IconSize="16">
    <ui:FluentIcon Symbol="Home24" />

    <StackPanel ui:IconPresenter.IconSize="20">
        <ui:FluentIcon Symbol="Settings24" />
    </StackPanel>
</StackPanel>
```

### 尺寸与粗细的优先级

`IconSize` 默认值为 `NaN`，`IconWeight` 默认值为 `null`。默认值表示图标呈现层不介入，
`FluentIcon` 完整保留原生 `FontSize` 和 `FontWeight` 的样式、继承、本地值、绑定与动画。

设置具体的 `IconSize` 或 `IconWeight` 后，图标语义属性优先于对应的原生字体属性。实现只
强制图标属性的有效值，不会替换其原始值来源；清除图标作用域后，原有绑定、样式或本地值
会立即恢复。

```csharp
IconPresenter.SetIconSize(toolbar, 16);
IconPresenter.SetIconWeight(toolbar, FontWeights.SemiBold);

// 恢复子树中每个图标原本的 WPF 字体值。
toolbar.ClearValue(IconPresenter.IconSizeProperty);
toolbar.ClearValue(IconPresenter.IconWeightProperty);
```

当前内置 Fluent 字体主要提供 Regular 与 Filled 两套字形，并不是完整的多字重字体族。
因此 `IconWeight` 的实际视觉差异取决于 WPF 字体匹配；该属性同时也是其它图标家族可以
接入的通用粗细语义。

## Regular、Filled 与 Variant

`Filled` 是图标或图标源自身的默认选择，`Variant` 是呈现位置发出的临时请求。两者按以下
规则组合：

| `Variant` | 最终形态 |
| --- | --- |
| `Default` | 使用 `Filled` 当前值 |
| `Regular` | 强制使用 Regular 字形 |
| `Filled` | 强制使用 Filled 字形 |

`Variant` 通过强制值改变 `Filled` 的有效值，不会删除本地值或绑定。当呈现位置恢复
`Default` 时，图标会重新使用自己的 `Filled` 值。

控件模板可以在不了解具体图标家族的情况下改变图标状态：

```xml
<ControlTemplate TargetType="{x:Type local:SelectableItem}">
    <ui:IconPresenter
        x:Name="IconPresenter"
        Content="{TemplateBinding Icon}" />
    <ControlTemplate.Triggers>
        <Trigger Property="IsSelected" Value="True">
            <Setter
                TargetName="IconPresenter"
                Property="ui:IconPresenter.Variant"
                Value="Filled" />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
```

`IconWeight` 与 `Variant` 的职责不同：前者表达粗细，后者选择 Regular 或 Filled 形态，
二者可以同时使用。

## 在文本中使用 FluentIconSpan

需要让图标参与一段文本的换行和基线排版时，使用 `FluentIconSpan`：

```xml
<TextBlock FontSize="14">
    <ui:FluentIconSpan
        Symbol="Document24"
        Filled="True"
        FontSize="16" />
    <Run Text=" README.md" />
</TextBlock>
```

`FluentIconSpan` 是 `Run`，会继承文本元素的字体属性，并默认将图标居中到文本基线。它支持
与 `FluentIcon` 相同的 `Symbol`、`Filled` 和 `Variant` 语义。当前图标呈现作用域的
`IconSize` 与 `IconWeight` 由独立的 `FluentIcon` 元素消费；行内图标应直接使用
`FontSize` 和 `FontWeight`。

## 属性速查

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `FluentIcon.Symbol` | `SymbolRegular` | `Empty` | 要显示的 Fluent 图标标识 |
| `FluentIcon.Filled` | `bool` | `false` | `Variant=Default` 时使用的默认形态 |
| `FluentIcon.Variant` | `IconVariant` | `Default` | 当前呈现位置请求的形态 |
| `FluentIcon.IconSize` | `double` | `NaN` | 图标专用尺寸；具体值优先于 `FontSize` |
| `FluentIcon.IconWeight` | `FontWeight?` | `null` | 图标专用粗细；具体值优先于 `FontWeight` |
| `IconPresenter.IconSize` | 附加 `double` | `NaN` | 向兼容图标子树传递尺寸 |
| `IconPresenter.IconWeight` | 附加 `FontWeight?` | `null` | 向兼容图标子树传递粗细 |
| `IconPresenter.Variant` | 附加 `IconVariant` | `Default` | 向兼容图标子树传递形态请求 |

## 自定义图标家族

`IconPresenter` 的呈现属性不是 Fluent 字体的私有协议。其它图标家族可以定义自己的不可变
`IconSource`，用隐式 `DataTemplate` 创建视觉元素，并通过 `AddOwner` 接入所支持的呈现
属性。无法表达某项语义的图标实现可以忽略它。

这种边界让控件模板只负责说明“这里的图标需要多大、以何种形态显示”，而不必识别
`FluentIcon`、替换应用绑定或查找模板内部的具体视觉元素。

## 可访问性建议

图标字体的字形文本不是有意义的无障碍名称。纯装饰图标应由包含它的按钮、菜单项或其它
交互控件提供可读的 `Content` 或 `AutomationProperties.Name`。仅显示图标的交互控件尤其
需要显式名称：

```xml
<ui:Button
    AutomationProperties.Name="打开设置"
    Icon="{ui:FluentIconSource Settings24}" />
```
