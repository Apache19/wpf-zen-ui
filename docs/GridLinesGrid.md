# GridLinesGrid 可自定义网格线

`GridLinesGrid` 继承 WPF `Grid`，通过两个独立的 `Pen` 配置横向和纵向网格线的颜色、宽度、虚线及端点样式。行列定义、子元素定位和跨行跨列的用法与 `Grid` 相同，不需要控件模板。

## 基本用法

在已有布局中将 `Grid` 换成 `ui:GridLinesGrid`，保留行列定义和子元素，并开启 `ShowGridLines`。下面的示例使用蓝色纵向实线和橙色横向虚线：

```xml
<ui:GridLinesGrid
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
    x:Name="SampleGrid"
    Height="180"
    ShowGridLines="True">
    <ui:GridLinesGrid.VerticalGridLinePen>
        <Pen Brush="DodgerBlue" Thickness="2" />
    </ui:GridLinesGrid.VerticalGridLinePen>
    <ui:GridLinesGrid.HorizontalGridLinePen>
        <Pen Brush="DarkOrange" Thickness="3" DashStyle="Dash" />
    </ui:GridLinesGrid.HorizontalGridLinePen>

    <ui:GridLinesGrid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </ui:GridLinesGrid.ColumnDefinitions>
    <ui:GridLinesGrid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </ui:GridLinesGrid.RowDefinitions>

    <TextBlock Margin="16" Text="名称" />
    <TextBlock Grid.Column="1" Margin="16" Text="说明" />
    <TextBlock Grid.Row="1" Margin="16" Text="项目 A" />
    <TextBlock Grid.Row="1" Grid.Column="1" Margin="16" Text="项目内容" />
</ui:GridLinesGrid>
```

## 属性与默认值

| 属性 | 默认值 | 用途 |
| --- | --- | --- |
| `ShowGridLines` | `false` | 继承自 `Grid`，控制所有网格线是否显示 |
| `HorizontalGridLinePen` | 灰色、1 DIP 宽的实线画笔 | 绘制内部行边界，即横向线 |
| `VerticalGridLinePen` | 灰色、1 DIP 宽的实线画笔 | 绘制内部列边界，即纵向线 |

两项画笔属性均为依赖属性，支持绑定、样式和资源引用。`Thickness` 使用设备无关单位 DIP，1 DIP 为 1/96 英寸，不保证等于一个物理像素。

将某一方向的画笔设为 `{x:Null}`、将其 `Brush` 设为 `{x:Null}`，或将其 `Thickness` 设为 `0`，均可隐藏该方向的网格线。例如，仅保留纵向网格线：

```xml
<ui:GridLinesGrid
    ShowGridLines="True"
    HorizontalGridLinePen="{x:Null}">
    <!-- 在这里添加列定义和子元素。 -->
</ui:GridLinesGrid>
```

后续 XAML 片段沿用基本示例中的命名空间声明。

## 画笔样式

直接使用 WPF `Pen` 的属性配置外观：

| Pen 属性 | 用途 |
| --- | --- |
| `Brush` | 线条画刷；可使用纯色、渐变等 WPF 画刷 |
| `Thickness` | 线条宽度 |
| `DashStyle` | 实线、虚线、点线或自定义虚线序列 |
| `DashCap` | 虚线段的端点形状，例如 `Flat` 或 `Round` |
| `StartLineCap`、`EndLineCap` | 整条线的起点和终点形状 |

内置 `DashStyle` 可选 `Solid`、`Dash`、`Dot`、`DashDot` 和 `DashDotDot`。自定义虚线可写成：

```xml
<ui:GridLinesGrid.HorizontalGridLinePen>
    <Pen Brush="DarkOrange" Thickness="2" DashCap="Flat">
        <Pen.DashStyle>
            <DashStyle Dashes="4,2,1,2" Offset="0" />
        </Pen.DashStyle>
    </Pen>
</ui:GridLinesGrid.HorizontalGridLinePen>
```

`Dashes` 交替指定实线段和间隔，其数值是线宽的倍数。上例在线宽为 2 DIP 时，依次为 8 DIP 实线、4 DIP 间隔、2 DIP 实线、4 DIP 间隔，并重复这一序列。`Offset` 控制虚线序列的起始偏移，同样以线宽为单位；端点形状会影响可见线段的长度。

## 运行时更新

替换画笔，或修改可变画笔及其画刷的子属性后，网格线会自动更新，无需手动调用 `InvalidateVisual`。可以在 XAML 中将画笔宽度绑定到滑块：

```xml
<StackPanel>
    <Slider x:Name="LineThickness" Minimum="0" Maximum="8" Value="2" />
    <ui:GridLinesGrid Height="120" ShowGridLines="True">
        <ui:GridLinesGrid.VerticalGridLinePen>
            <Pen
                Brush="DodgerBlue"
                Thickness="{Binding Value, ElementName=LineThickness}" />
        </ui:GridLinesGrid.VerticalGridLinePen>
        <ui:GridLinesGrid.ColumnDefinitions>
            <ColumnDefinition />
            <ColumnDefinition />
        </ui:GridLinesGrid.ColumnDefinitions>
    </ui:GridLinesGrid>
</StackPanel>
```

默认画笔已冻结并由控件实例共享。需要在 C# 中调整画笔时，应创建新画笔，或先克隆当前画笔再赋回属性。下面的代码用于基本示例中的 `SampleGrid`：

```csharp
using System.Windows.Media;

var brush = new SolidColorBrush(Colors.DodgerBlue);
var pen = new Pen(brush, 2);
SampleGrid.VerticalGridLinePen = pen;

// 在所属 UI 线程上更新可变画笔及其画刷。
pen.Thickness = 4;
brush.Color = Colors.MediumSeaGreen;

SampleGrid.HorizontalGridLinePen = null;
SampleGrid.ShowGridLines = false;
```

共享一个可变画笔时，修改它会影响所有引用它的控件。需要独立外观的控件应分别创建或克隆画笔。

## 绘制与布局行为

- 网格线沿实际行列边界绘制。`Auto`、固定尺寸、星号尺寸、共享尺寸、布局舍入和 `FlowDirection` 仍由 WPF `Grid` 处理；调整尺寸或增删行列后，线条跟随布局更新。
- 只绘制内部边界，不绘制外框。某一方向没有定义或只有一个定义时，该方向没有内部网格线。需要外框时，可在外层使用 `Border`。
- 网格线位于子元素上方，即使子元素具有不透明背景或较高的 `Panel.ZIndex`，也会显示在其上方。
- `Grid.RowSpan` 和 `Grid.ColumnSpan` 不会中断网格线，线条仍穿过跨行跨列区域。
- 网格线不占布局空间，线宽以边界为中心向两侧延伸。需要让内容避开线条时，应设置子元素的 `Margin` 或内容容器的 `Padding`。
- 先绘制纵向线，再绘制横向线。交叉位置按 WPF 的正常绘图规则叠加；使用半透明画刷时，交叉位置可能更深。

## 图库示例与实现位置

图库的 Layout 分类提供 `GridLinesGrid` 示例，可切换网格线开关、拖动滑块调整纵向线宽，并观察跨列内容上的绘制效果。

- [控件实现](../src/Wpf.Zen.Ui/Controls/GridLinesGrid/GridLinesGrid.cs)
- [图库示例](../src/Wpf.Zen.Ui.Gallery/Views/Pages/Layout/GridLinesGridPage.xaml)
- [渲染回归测试](../tests/Wpf.Zen.Ui.UnitTests/Controls/GridLinesGridTests.cs)

实现复用 WPF `Grid` 的网格线 `DrawingVisual`，在布局和绘制阶段替换其绘图内容。维护时需注意：此实现依赖 WPF 将网格线绘图层暴露为最后一个视觉子节点；升级 WPF 后，应运行渲染回归测试确认这一行为。
