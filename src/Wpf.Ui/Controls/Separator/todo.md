TODO：实现自适应 Separator 样式

为 Separator 增加一套自适应样式，使其能够根据上层布局容器的方向自动切换显示形态。

实现思路：

* 不直接依赖 `Parent.Orientation`，因为 Separator 可能被包裹在 `Border`、`Grid`、ContentControl` 等嵌套布局中。
* 通过附加属性实现一个 `SeparatorAssist.AutoOrientation` 行为。
* 在控件 `Loaded` 时，从当前 Separator 开始向上遍历逻辑树/可视树，查找最近的方向型容器。
* 初期支持：

  * `StackPanel.Orientation`
  * `WrapPanel.Orientation`
* 找到最近的方向型容器后，将其方向写入只读/内部使用的附加属性，例如 `SeparatorAssist.DetectedOrientation`。
* Separator 样式通过 `DataTrigger` 监听 `DetectedOrientation`：

  * 父容器为 `Vertical` 时，Separator 显示为横线。
  * 父容器为 `Horizontal` 时，Separator 显示为竖线。
* 默认方向为 `Vertical`，即默认显示横向分隔线。
* 支持运行时动态切换父容器方向，考虑监听父级 `Orientation` 属性变化或增加手动刷新机制。

目标：移除 ui:Separator，让原生 Separator 即使处于多层嵌套布局中，也能自动适配最近的 StackPanel / WrapPanel 等带有 Orientation 属性的控件的方向。
