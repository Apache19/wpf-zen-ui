# 平滑滚动与虚拟化

`PassiveScrollViewer` 默认对支持像素偏移的内容启用纵向鼠标滚轮平滑滚动，`DynamicScrollViewer` 同样具备此能力。

```xml
<ui:PassiveScrollViewer SmoothScrollDuration="0:0:0.240">
    <!-- 普通内容 -->
</ui:PassiveScrollViewer>
```

`IsSmoothScrollingEnabled="False"` 可以关闭平滑滚动。Windows 关闭客户端区域动画时会自动使用原生行为；`ScrollSpillMode="Native"` 也完全遵循原生滚轮行为。

## 运动模型

控件维护当前位置、目标位置和速度，在 `CompositionTarget.Rendering` 中按实际经过时间推进临界阻尼运动：

```text
加速度 = ω² × (目标位置 − 当前位置) − 2ω × 速度
ω = 6.64 / SmoothScrollDuration.TotalSeconds
```

采用该方程的解析解，刷新率变化或一帧延迟不会造成数值积分发散。默认 240 ms 表示单次滚轮输入约完成 99% 路程的响应时间，剩余尾段在位置和速度足够小时精确归位。

- 连续同向输入累加目标距离，保留位置和速度，自然产生加速。
- 反向输入清除尚未完成的旧方向路程和速度，立即响应新方向。
- 按滚轮 `Delta / 120` 保留高分辨率滚轮的小增量，同时读取 Windows 每次滚动行数、整页滚动和禁用滚轮的设置；物理行距采用 WPF 的 16 DIP。
- 目标始终限制在有效范围内，不产生边界回弹。嵌套滚动按待到达的目标判断边界，避免内部视图减速时吞掉后续滚轮事件；单个事件不会拆分为内外两份剩余距离。
- 键盘、鼠标按下、触摸、滚动命令和 `BringIntoView` 会停止当前运动。程序设置偏移产生实际变化时也会接管滚动。
- 卸载、隐藏、禁用、替换内容或模板、修改平滑滚动配置会清理渲染事件订阅。
- 最后一帧提交后，等待布局确认最终偏移再结束运动；布局取整未产生偏移事件时，下一帧完成清理，避免尺寸变化把最后一步滚动撤回。

## 保留列表虚拟化

`IScrollInfo` 没有公开的单位查询接口。控件识别 `ScrollContentPresenter`、像素模式的 `VirtualizingStackPanel` 和本库 `VirtualizingWrapPanel`；横向堆叠面板的纵向交叉轴也是像素坐标。按项滚动和未知滚动提供程序使用原生滚轮行为。

列表需要在项目控件上选择像素单位，同时保持 `CanContentScroll="True"`：

```xml
<ListBox
    ScrollViewer.CanContentScroll="True"
    VirtualizingPanel.IsVirtualizing="True"
    VirtualizingPanel.VirtualizationMode="Recycling"
    VirtualizingPanel.ScrollUnit="Pixel"
    ui:PassiveScrollViewer.IsSmoothScrollingEnabled="True"
    ui:PassiveScrollViewer.SmoothScrollDuration="0:0:0.240" />
```

上述写法需要使用本库含 `PassiveScrollViewer` 的控件样式。平滑滚动配置会从模板所属控件传给内部视图；直接设置在视图及其样式上的值优先，配置不通过属性继承影响内容中的独立嵌套视图。

不要为平滑滚动把虚拟化列表的 `CanContentScroll` 改为 `False`：这会把滚动职责交给外层内容呈现器，失去面板所需的有效虚拟化视口。控件不会擅自修改该属性或 `VirtualizingPanel.ScrollUnit`。

自定义像素 `IScrollInfo` 可以通过派生 `PassiveScrollViewer` 并覆写 `SupportsSmoothScrolling` 明确声明支持。该提供程序必须接受连续 DIP 偏移，正确限制范围并通知 `ScrollOwner`；不能对按项取整的提供程序返回 `true`。

Gallery 的 **VirtualizingWrapPanel / Movie library** 页面提供 **Smooth scrolling** 开关，可以在同一份虚拟化内容上直接对比。

## WPF 机制与能力边界

实现依据本地 WPF 源码的 `ScrollViewer.OnMouseWheel`、`ScrollViewer.OnLayoutUpdated`、`ScrollContentPresenter.SetVerticalOffset`、`VirtualizingStackPanel.SetVerticalOffset` 和 `MediaContext.RenderMessageHandlerCore`：

1. 原生滚轮直接调用滚动提供程序；`ScrollToVerticalOffset` 则先经过命令队列，并在布局阶段执行。
2. 每帧直接通过公开的 `IScrollInfo.SetVerticalOffset` 提交连续位置，避免为每帧额外排队。
3. WPF 在 `Rendering` 事件后再次处理布局失效，再提交本帧画面。因此不需要强制 `UpdateLayout()`，也不需要重启 `DoubleAnimation` 或反射内部状态。

这里移动的是真实滚动偏移，滚动条、命中测试、自动化和虚拟化继续使用一致的坐标。虚拟化面板修正内容范围时会同步限制运动目标，内容缩小后不会保留越界目标。如果布局发布的偏移偏离本帧请求及其合法范围修正，控件会停止运动，让新的偏移接管；布局取整允许一个物理像素的误差。由于 `IScrollInfo` 不公开偏移变化来源，额外的布局锚点跳转也按接管处理，避免沿着旧坐标继续拉动内容。

这仍然是 **WPF UI 线程上的滚动**。布局、内容生成或业务代码阻塞 UI 线程时，无法像独立合成线程的滚动那样继续流畅更新。仅替换 `ScrollViewer` 无法消除这一架构限制；对任意内容只加渲染平移又会绕开真实滚动布局和项目实现范围。本实现不宣称解决主线程阻塞，也不替换触摸操作、横向滚轮输入或原生逻辑滚动协议。

原生编程滚动 API 不是虚方法；对于把偏移再次设为当前值、完全不产生 `ScrollChanged` 的调用，控件无法通过公开事件区分它与无操作。需要显式终止运动时，可以关闭 `IsSmoothScrollingEnabled`。
