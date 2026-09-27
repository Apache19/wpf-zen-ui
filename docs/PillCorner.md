# 胶囊圆角 PillCornerConverter

`PillCornerConverter` 是直接用于目标依赖属性的标记扩展。默认观察元素自身的 `ActualWidth` 和 `ActualHeight`，按短边的一半生成圆角；布局、尺寸动画和窗口缩放导致实际尺寸变化时自动更新。

```xml
xmlns:conv="clr-namespace:Wpf.Zen.Ui.Converters;assembly=Wpf.Zen.Ui"

<Border CornerRadius="{conv:PillCornerConverter}" />
<Border CornerRadius="{conv:PillCornerConverter Corners=Left}" />
<Border CornerRadius="{conv:PillCornerConverter Corners='TopLeft,BottomRight'}" />
```

无需声明转换器资源或手写 MultiBinding。内部使用 WPF 的尺寸依赖属性通知；普通 `Binding Self` 只观察元素引用，不能自动跟随尺寸变化，因此不要将此扩展放进 `Binding.Converter`。

## 拼接独立交互区域

横向拼接时，指定 `Dimension=Height`，可以让不同宽度的区域使用同一个半径。中间区域保持直角。以下示例中的 `ui` 指向 `Wpf.Zen.Ui.Controls`。

```xml
<StackPanel Orientation="Horizontal" Height="40">
    <ui:Button Content="上一项"
        CornerRadius="{conv:PillCornerConverter Corners=Left, Dimension=Height}" />
    <ui:Button Content="选择" CornerRadius="0" />
    <ui:Button Content="下一项"
        CornerRadius="{conv:PillCornerConverter Corners=Right, Dimension=Height}" />
</StackPanel>
```

竖向拼接对应 `Dimension=Width` 和 `Corners=Top` / `Bottom`。这些方位是物理方位，不随 `FlowDirection` 翻转。若区域过窄，WPF 会按可用几何空间缩放圆角；横向端部通常至少需要半个高度的宽度。

也可以让多个元素观察同一个容器：

```xml
<Grid x:Name="PillHost" Height="40">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <ui:Button Content="操作"
        CornerRadius="{conv:PillCornerConverter ElementName=PillHost, Corners=Left}" />
    <ui:Button Grid.Column="1" Content="详情"
        CornerRadius="{conv:PillCornerConverter ElementName=PillHost, Corners=Right}" />
</Grid>
```

模板内可用 `RelativeSource={RelativeSource TemplatedParent}`；也支持 `FindAncestor`。`ElementName` 和 `RelativeSource` 只能指定一个，均省略时使用 `Self`。源元素需要提供 `ActualWidth`、`ActualHeight`。

## 半径规则

计算顺序是：选择尺寸 → 乘以 `Factor` → 加上 `Offset` → 限制在 `[MinRadius, MaxRadius]` 内 → 应用角选择。

| 属性 | 默认值 | 用途 |
| --- | --- | --- |
| `Corners` | `All` | 复用 `CornerRadiusCorners`；支持四个单角、四条边、任意组合、`None` |
| `Dimension` | `ShorterSide` | `ShorterSide`、`Width`、`Height` |
| `Factor` | `0.5` | 有限非负比例；减小可减弱圆角 |
| `Offset` | `0` | 有限偏移，单位为 DIP；允许负值，适用于内缩轮廓 |
| `MinRadius` | `0` | 有限非负下限，只作用于选中的角 |
| `MaxRadius` | 正无穷 | 上限，不能小于下限；`0` 表示圆角为零 |

实际宽高任一为零或不可用时，输出零，不应用最小半径。无效配置在创建绑定时抛出异常。计算结果始终为有限非负数；WPF 仍会根据元素几何空间调整实际绘制。目标支持 `CornerRadius` 和 `double`；标量目标忽略 `Corners`，可用于阴影等单半径属性。

```xml
<Border CornerRadius="{conv:PillCornerConverter Factor=0.4, Offset=-1, MaxRadius=24}" />
```

## 样式与代码

```xml
<Style TargetType="Border">
    <Setter Property="CornerRadius" Value="{conv:PillCornerConverter Corners=Top}" />
</Style>
```

每个样式或模板实例使用自身的尺寸绑定，不保存对目标元素的事件订阅。配置在创建绑定时被捕获；修改扩展对象的属性不会修改已安装的绑定。

```csharp
var options = new PillCornerConverter
{
    Corners = CornerRadiusCorners.Left,
    Dimension = PillCornerDimension.Height,
};
BindingOperations.SetBinding(border, Border.CornerRadiusProperty, options.CreateBinding());
```

原有手写 `MultiBinding Converter="{StaticResource PillCornerConverter}"` 用法改为在圆角属性上直接使用上述扩展。`MaxRadius=0` 现在表示零上限，无上限使用默认值。
