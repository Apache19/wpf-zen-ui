# 带间距的堆叠面板

`UiStackPanel` 和 `UiVirtualizingStackPanel` 位于 `Wpf.Zen.Ui.Controls`，可通过公共 XAML 命名空间使用：

```xml
xmlns:ui="http://schemas.thezendev.com/wpf/2027"
```

**虚拟化仅支持平面列表。** `UiVirtualizingStackPanel` 不支持分层虚拟化；分组和树形场景的完整布局及使用边界见下文。

## 普通布局

```xml
<ui:UiStackPanel Orientation="Horizontal"
                 Spacing="12"
                 SpacingBrush="#40808080">
    <Button Content="保存" />
    <Button Content="取消" />
</ui:UiStackPanel>
```

| 属性 | 默认值 | 含义 |
| --- | --- | --- |
| `Orientation` | `Vertical` | 沿纵向或横向堆叠 |
| `Spacing` | `0` | 相邻布局槽之间的间距，单位 DIP，必须有限且非负 |
| `SpacingBrush` | `null` | 间距的填充画刷，支持纯色、渐变和其他 WPF 画刷 |
| `Background` | `null` | 面板整体背景，沿用 WPF `Panel` 的行为 |

间距参加测量和排列，不修改子项的 `Margin`、尺寸或绑定。`SpacingBrush` 绘制在背景之上、子项之下，只填充间距，不填充子项内部的边距和对齐空白。绘制覆盖面板的横截面，并裁剪到面板范围内。

首尾没有额外间距。`Collapsed` 项不占空间，也不产生间距；`Hidden` 项仍占空间。零尺寸但未折叠的项仍有布局槽，参与间距计算。更改间距、方向、子项尺寸或可见性后，布局及绘制自动更新。

`UiStackPanel` 继承 `Panel`，适合直接添加子元素，或放入普通 `ItemsControl.ItemsPanel`。它测量所有子项；外层 `ScrollViewer` 对整个面板进行物理滚动。

## 虚拟化列表

```xml
<ListBox Height="320"
         ItemsSource="{Binding Items}"
         ScrollViewer.CanContentScroll="True"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"
         VirtualizingPanel.ScrollUnit="Pixel">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <ui:UiVirtualizingStackPanel Spacing="8"
                                        SpacingBrush="#40808080" />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
```

`UiVirtualizingStackPanel` 继承 `VirtualizingPanel` 并实现 `IScrollInfo`，自行将间距纳入尺寸估计、可见区计算、容器回收和滚动定位。它支持不同尺寸的项，无需指定固定项高。两种面板不是 WPF 同名类型的派生类，面向它们的样式应使用各自的 `TargetType`。

- `ScrollUnit="Pixel"` 使用 DIP，适合连续滚动；`ScrollUnit="Item"` 在堆叠方向使用项单位。末端允许小数项偏移，使最后一项与视口末端对齐。横截面方向始终使用 DIP。
- `VirtualizationMode` 支持 `Standard` 和 `Recycling`。`CacheLength` 与 `CacheLengthUnit` 支持项数、像素和页面三种缓存单位。
- `ScrollIntoView` 可生成尚未显示的项；`MakeVisible` 支持已生成的后代元素及双轴定位。
- 含键盘焦点或设置 `VirtualizingPanel.IsContainerVirtualizable="False"` 的容器会保留。
- 回收前会在所属 `ItemsControl` 上触发 `CleanUpVirtualizedItem`，设置事件参数的 `Cancel=true` 可保留容器。本面板与原生面板共用该事件，已有的原生事件处理器继续生效；标准模式和回收模式均支持取消。
- `GetItemOffset` 返回直接子容器相对完整内容起点的偏移，不受当前滚动位置影响。像素模式包含间距，项模式只计入前面的未折叠布局槽，不计入槽内后代。
- 只对视口及缓存范围生成容器。尺寸缓存占用与数据项总数成正比的内存；查询位置和更新一个尺寸均为对数复杂度。
- 尚未测量的项使用已测量项的平均尺寸估计。滚动范围会随测量结果修正，面板通过可见项锚点减少估计变化导致的跳动。未生成项的内容或可见性变化，要在重新生成并测量时才能获知。
- 集合增删、移动、替换和重置会清理尺寸估计；不受本次变更影响的现有容器保留。视口锚点仍存在时，继续保持该项的局部滚动位置。

横向列表设置 `Orientation="Horizontal"` 并启用水平滚动条。虚拟化需要滚动方向上有限的视口；例如纵向列表应放入有限高度的 `Grid` 行，或设置 `Height`。不要用另一个纵向 `StackPanel` 的无限高度代替视口约束。

`IsVirtualizationActive` 是只读依赖属性，报告最近一次布局是否实际限制了容器生成范围。禁用虚拟化，或者最外层缺少有限的滚动视口时，会测量全部项。分组视图及 `TreeView` / `TreeViewItem` 宿主也使用完整布局，并报告 `IsVirtualizationActive=False`。

## 支持范围与使用边界

**`UiVirtualizingStackPanel` 仅支持平面列表虚拟化，不支持分层虚拟化，也不是原生 `VirtualizingStackPanel` 的完全替代品。**

| 场景 | 行为与建议 |
| --- | --- |
| 未分组的 `ListBox`、`ListView` 等平面列表 | 在有限视口及 `CanContentScroll=True` 下支持虚拟化、回收、间距和间距绘制 |
| 分组视图，包括多级分组 | 本面板完整生成当前面板的组或子项，不传递分层视口；即使开启 `IsVirtualizingWhenGrouping` 也不会启用分层虚拟化 |
| `TreeView` / `TreeViewItem` | 本面板采用完整布局；展开分支的后代不会获得分层虚拟化，折叠行为仍由 WPF 容器处理 |
| 原生与自定义面板混合的虚拟化层级 | 不支持，不保证跨层滚动定位、尺寸报告或虚拟化行为 |
| 每个列表拥有独立 `ScrollViewer` 和有限视口 | 各列表可以独立进行平面虚拟化，这不属于共享外层视口的分层虚拟化 |

完整布局可能生成大量容器，不能将分组或树形场景中的完整布局当作大数据量虚拟化方案。需要分组或树形虚拟化时，应使用 WPF 原生 `VirtualizingStackPanel`；如需使用本面板处理大数据量，可将数据转换为平面列表后绑定。

`CanHierarchicallyScrollAndVirtualize` 始终返回 `False`。`IsVirtualizationActive` 仅表示当前面板是否正在限制直接子容器的生成范围，不表示后代列表也在虚拟化。平面列表的 `ScrollIntoView` / `MakeVisible` 受支持；分组和树形的跨层定位不属于本面板的支持契约。

本面板继承 `VirtualizingPanel`，不能沿用以原生 `VirtualizingStackPanel` 为 `TargetType` 的样式或派生类扩展。项滚动、缓存和尺寸估计采用本面板的实现，不保证与原生所有行为等价。

Gallery 的 **Collections → Stack panels** 提供普通堆叠和 10,000 个不同高度项的平面列表示例，并在虚拟化示例旁说明上述边界。

## 实现阅读顺序

| 文件 | 职责 |
| --- | --- |
| `UiVirtualizingStackPanel.cs` | 控件属性、测量入口、排列及间距绘制 |
| `UiVirtualizingStackPanel.Layout.cs` | 测量准备与收尾、锚点选择、可见区和缓存范围计算 |
| `UiVirtualizingStackPanel.Containers.cs` | 生成器与视觉子项的同步、清理事件、回收及集合变更 |
| `UiVirtualizingStackPanel.Scrolling.cs` | `IScrollInfo`、滚动命令、定位及像素/项单位转换 |
| `StackItemMetrics.cs` | 尺寸估计、前缀和及位置查询 |

从 `MeasureOverride` 开始阅读：准备测量状态 → 选择直接子项、完整生成或虚拟化测量 → 更新滚动范围和期望尺寸。虚拟化测量以 `LayoutAnchor` 保存项索引及项内像素偏移，以 `CacheRange` 表示生成范围；测量结果修正估计后，如果当前范围不足或滚动位置被约束到新边界，就重新计算范围。

内部 `_stackOffset` / `_stackViewport` 始终使用 DIP，`stack` 表示堆叠方向，`cross` 表示垂直于堆叠的方向。项单位仅在滚动接口边界转换。`_itemSizes` 保存物理长度，`_itemCounts` 保存未折叠槽的逻辑计数，二者共享同一数据索引。
