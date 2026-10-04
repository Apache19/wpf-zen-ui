# 字体图标与呈现

字体图标分为共享的描述数据和独立的呈现元素。系列差异集中在 Source 中，宿主只消费
字符、字体和通用呈现请求。

| 类型 | 职责 |
| --- | --- |
| `FontIconSource` | 不可变的字体图标描述契约，提供 `Glyph` 和 `FontFamily` |
| `FluentIconSource` | Fluent 的 Symbol、Filled、字符映射与字体选择 |
| `FontIcon` | 基于 `TextBlock` 的通用独立呈现，通过 `Source` 选择图标 |
| `FontIconSpan` | 基于 `Run` 的通用行内呈现，通过 `Source` 选择图标 |
| `IconPresenter` | 控件图标槽的通用内容宿主，传递尺寸、颜色和状态变体 |

## 直接显示

```xml
<StackPanel xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <ui:FontIcon Source="{ui:FluentIconSource Home24}" FontSize="24" />
    <ui:FontIcon Source="{ui:FluentIconSource Star24, Filled=True}" FontSize="32" />
    <ui:FontIcon Source="{Binding CurrentIcon}" Foreground="CornflowerBlue" />
</StackPanel>
```

`FontIcon` 没有 Symbol 或 Filled 等属性。所有系列特有选项由其 Source 表达；`FontSize`、
`FontWeight`、`Foreground`、布局和数据上下文使用 WPF 原生行为。字符和有效字体由 Source
管理，不应再通过宿主的 `Text`、`Inlines` 或 `FontFamily` 选择图标。

```csharp
var source = new FluentIconSource(FluentSymbolRegular.Home24, filled: true);
var icon = new FontIcon(source) { FontSize = 24 };

// 替换描述即可切换图标，也可以换成另一个系列的 FontIconSource。
icon.Source = new FluentIconSource(FluentSymbolRegular.Settings24);
```

Source 不可变，可在资源、视图模型、样式和多个宿主间共享。更新绑定图标时，视图模型替换
Source 并发出属性更改通知。绑定宿主的 `Source` 即可，无需让 Source 依附某个视觉树。
`Source=null` 显示空内容；`FluentSymbolRegular.Empty` 同样转换为空字符串。

## 隐式模板与控件图标槽

库的 `Generic.xaml` 为 **`FontIconSource` 基类**注册隐式数据模板：

```xml
<DataTemplate DataType="{x:Type ui:FontIconSource}">
    <ui:FontIcon Source="{Binding}" />
</DataTemplate>
```

合并库样式后，普通 `ContentPresenter` 能使用该模板呈现任意派生字体源。每个宿主创建自己的
`FontIcon`，而 Source 可以共享。未来新增字体系列无需另写视觉控件和默认数据模板。
显式 `ContentTemplate`、模板选择器和应用定义的隐式模板仍按 WPF 原生规则选择。

```xml
<ui:Button Content="Home" Icon="{ui:FluentIconSource Home24}" />
<ContentControl Content="{ui:FluentIconSource Star24, Filled=True}" />
<Setter Property="Icon" Value="{ui:FluentIconSource Settings24}" />
```

图标槽仍接受任意 WPF 内容：字体图标源、图片、路径或自定义数据均可。非字体的图标描述
可以继承 `IconSource` 并提供自己的数据模板。

## 行内图标与徽章

```xml
<TextBlock xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <ui:FontIconSpan Source="{ui:FluentIconSource Document24}" FontSize="16" />
    <Run Text=" Document" />
</TextBlock>

<ui:Badge Icon="{ui:FluentIconSource Checkmark16, Filled=True}" Severity="Success" />
```

`FontIconSpan` 参与文本换行和基线排版，默认 `BaselineAlignment=Center`。它的字号、字重
和颜色遵循文本继承，也可以直接设置。`IconSize`、`IconWeight`、`IconForeground` 作用域由
独立的 `FontIcon` 消费；行内图标使用所在段落的文本外观。

`Badge.Icon` 接受任意 `FontIconSource`。徽章读取源的 `Glyph` 和字体，结合自身字号、前景色、
Padding 和内容对齐绘制；不创建 `FontIcon`。设置 Icon 后优先显示图标，清空后恢复 Text。

## 尺寸与颜色作用域

`IconPresenter` 提供可继承的图标专用外观属性，不影响相邻普通文字：

| 属性 | 默认值 | 行为 |
| --- | --- | --- |
| `IconSize` | `NaN` | 具体值覆盖 FontIcon 的有效 FontSize |
| `IconWeight` | `null` | 具体值覆盖 FontIcon 的有效 FontWeight |
| `IconForeground` | `null` | 具体画刷覆盖 FontIcon 的有效 Foreground |
| `Variant` | `Default` | 请求 Source 解释呈现变体 |

```xml
<StackPanel ui:IconPresenter.IconSize="20"
            ui:IconPresenter.IconForeground="CornflowerBlue">
    <ui:FontIcon Source="{ui:FluentIconSource Home24}" />
    <TextBlock Text="普通文字不受图标外观作用域影响" />
</StackPanel>
```

`FontIcon` 通过强制值合并图标作用域与原生文本外观，不替换原生属性的绑定、本地值或动态
资源。清除作用域后，原有文本属性值恢复。`IconWeight` 的实际效果取决于字体提供的字重；
它与 Regular / Filled 的字体选择相互独立。

## 状态变体

`Variant` 是宿主发出的通用状态请求。`FontIcon` 和 `FontIconSpan` 调用
`Source.WithVariant(Variant)`，使用返回描述进行呈现，同时保留原始 Source 和绑定。

Fluent 的解释如下：

| Variant | 呈现 |
| --- | --- |
| `Default` | 使用 Source 自己的 Filled |
| `Regular` | 使用 Regular 字符和字体 |
| `Filled` | 使用 Filled 字符和字体 |

控件模板可以在选中时设置 `IconPresenter.Variant=Filled`，取消选中时恢复 Default。
同一个 Source 可同时在两个宿主中显示不同形态，不会被修改。Badge 直接使用所赋 Source
自己的形态。其它字体系列可覆盖 `WithVariant`；不支持的请求默认返回自身。
系列独有的其它变体通过具体 Source 的构造参数表达，不需要修改通用控件或扩展此枚举。

## 字体资源与扩展

Source 的 `FontFamily` 必须提供独立于宿主、可用的默认字体。可选的 `FontFamilyResourceKey`
允许宿主从自身作用域解析替代字体。`FontIcon`、`FontIconSpan` 和 `Badge` 使用 WPF 动态资源
跟随资源替换和重新挂接；找不到资源时回退到 Source 的字体。替代字体需要包含相应 Glyph。
Source 自身不缓存应用或窗口中的资源。

Fluent 的默认字体统一来自 `Resources/Fonts.xaml`，由 `FluentIconSource` 首次使用时加载。
Regular / Filled 分别使用 `FontFamilyKeys.FluentSystemIcons` 和
`FontFamilyKeys.FluentSystemIconsFilled`。字符映射和字体选择都由 `FluentIconSource` 负责。

最小的自定义字体系列只需实现两个属性：

```csharp
/// <summary>
/// Describes an immutable icon from an application's symbol font.
/// </summary>
public sealed class AppIconSource : FontIconSource
{
    /// <inheritdoc />
    public override string Glyph => "★";

    /// <inheritdoc />
    public override FontFamily FontFamily { get; } = new FontFamily("Segoe UI Symbol");
}
```

`Glyph` 是 Unicode 字符串，不是字体内部的 glyph index；也可以包含代理对。封装字体文件时
应提供正确的字体基址与字体族名。通过 Source 返回正确字体后，所有宿主共用同一套定义。
按需要增加自己的枚举、变体构造参数和标记扩展即可；标记扩展返回 Source 数据。

Fluent 字符表位于 `Controls/Icon`：`FluentSymbolRegular` 和 `FluentSymbolFilled`。
`FluentIconSource` 接收 `FluentSymbolRegular`，根据 `Filled` 按名称映射到对应字体的码点，
并通过 `Glyph` 返回完整 Unicode 文本（包括需要代理项对的字符）；`Empty` 返回空字符串。
