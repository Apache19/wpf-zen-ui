# 带间隔的停靠面板

`Wpf.Zen.Ui.Controls.UiDockPanel` 派生自 WPF 原生 `DockPanel`，增加参与布局的间隔与间隔染色。

```xml
<ui:UiDockPanel xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                Spacing="8"
                SpacingBrush="#40808080">
    <Border DockPanel.Dock="Top" Height="48" Background="LightBlue" />
    <Border DockPanel.Dock="Left" Width="160" Background="LightGray" />
    <Border Background="White" />
</ui:UiDockPanel>
```

| 属性 | 默认值 | 含义 |
| --- | --- | --- |
| `Spacing` | `0` | 停靠槽与后续区域之间的距离，单位 DIP，必须有限且非负 |
| `SpacingBrush` | `null` | 间隔的填充画刷，支持纯色、渐变等 WPF 画刷 |
| `LastChildFill` | `true` | 继承原生行为，集合中最后一个子元素填满剩余区域 |
| `Background` | `null` | 继承原生面板背景行为 |

子元素按集合顺序停靠：Left/Right 在靠近剩余区域的一侧留下水平间隔，Top/Bottom 留下垂直间隔。间隔沿当时剩余区域的横截面延伸。因此示例中顶部元素下方的间隔横贯面板，左侧元素右方的间隔从顶部区域下方开始。

间隔参加测量和排列，叠加在子元素原有 Margin 之外，不修改子元素属性或绑定。绘制位于面板背景之上、子元素之下，只填充间隔区域，不填充 Margin、对齐空白或未使用的剩余区域。间隔绘制受对应剩余区域和面板边界限制；渐变等相对坐标画刷以整个间隔图形为坐标范围。

只有后面仍有未折叠子元素时才产生间隔。面板外缘和最后一个参与布局的子元素之后没有额外间隔。`Collapsed` 不占空间、不产生间隔，`Hidden` 和零尺寸子元素仍参与间隔计算。间隔、画刷、停靠方向、子元素尺寸、可见性及集合变化都会更新布局或绘制。

`LastChildFill` 与原生 DockPanel 一致，针对**集合中的最后一个子元素**。如果最后一个元素折叠，填充职责不会自动转移到前一个元素。设置 `LastChildFill="False"` 后，所有子元素均按自身 Dock 值排列，未使用的剩余区域仍显示 Background。
