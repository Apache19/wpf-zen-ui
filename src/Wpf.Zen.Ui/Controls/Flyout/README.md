# Flyout

`Flyout` 是一个独立的内容弹层。它继承自 `ContentControl`，可以直接设置内容、模板和样式，也可以继续派生为业务专用控件。控件内部拥有原生 WPF `Popup`，因此 Flyout 本身应放在资源、附加属性或弹层属性中，不能作为普通布局子元素使用。

## 显式打开

```xaml
<Page.Resources>
    <ui:Flyout x:Key="DetailsFlyout" Placement="Bottom">
        <local:DetailsView />
    </ui:Flyout>
</Page.Resources>
```

```csharp
((Flyout)Resources["DetailsFlyout"]).ShowAt(anchor);
```

`ShowAt` 同时设置目标并打开弹层；`Show` 使用已有的 `PlacementTarget`；`Hide` 关闭弹层。这些方法使用依赖属性的当前值更新，不会移除现有绑定。

## 焦点或点击触发的弹层

选择器、检查器等弹层可以使用 `FlyoutService`，无需手工订阅焦点或鼠标事件：

```xaml
<ui:TextBox ui:FlyoutService.ShowOnFocusOrClick="True">
    <ui:FlyoutService.AttachedFlyout>
        <ui:Flyout Placement="Bottom">
            <local:SuggestionView />
        </ui:Flyout>
    </ui:FlyoutService.AttachedFlyout>
</ui:TextBox>
```

键盘焦点进入 TextBox 或主鼠标键完成一次点击时弹层打开；弹层已经打开时再次点击不会切换、重建或重复打开。鼠标触发特意等到 MouseUp 后才打开，避免 TextBox 的选区捕获阻止 Popup 建立外部点击捕获。这样外部点击能够正常关闭，ESC 关闭后即使 TextBox 仍保持焦点，下一次点击也能重新打开。右键不参与此行为，仍由 WPF 的 ContextMenu 机制处理。

`AttachedFlyout` 也可以只作为所有权声明，再通过 `FlyoutService.ShowAttachedFlyout(element)` 和 `HideAttachedFlyout(element)` 手工控制。一个 Flyout 可以由多个目标共享，服务会在打开时重新选择目标，并防止旧目标的延迟焦点回调关闭已经转移的弹层。

## 焦点进入弹层

`FocusOnOpen` 默认为 `true`。这种模式适合命令面板和选择器：打开时焦点移动到第一个可聚焦内容，Tab 在弹层内部循环，关闭时在安全条件下回到目标。焦点在目标与弹层之间移动不会被 `FlyoutService` 误判为离开整个范围，ESC 关闭后恢复到目标也不会立即重新打开。

输入建议等必须继续在宿主中输入的场景可以显式设置 `FocusOnOpen="False"`。此时输入、光标导航和选择留在 TextBox 中，但 ESC、F4、Alt+Up 和 Alt+Down 仍可关闭弹层，外部点击仍由 Popup 的捕获机制关闭。面板中若有需要键盘操作的内容，应用应另外提供进入面板的明确键盘路径。

## 上下文菜单与输入边界

Flyout 会阻止普通鼠标点击和滚轮继续冒泡到背后的触发器或页面，但会保留鼠标右键事件。WPF 的 `PopupControlService` 依赖未处理的右键抬起事件来创建 TextBox、RichTextBox 等编辑器的标准上下文菜单；因此面板内的右键菜单可以同时通过鼠标和键盘菜单键打开。嵌套 `ContextMenu` 仍由 WPF 自己管理捕获与关闭。

## 派生控件

Flyout 不是密封类型。派生类型会继续使用基础 Flyout 样式和完整的 Popup、焦点、关闭及目标生命周期机制；可以覆盖 `OnOpened`、`OnClosed`，或为派生类型提供专用模板。派生实现不应自行创建第二个 Popup，也不应把 Flyout 内容放入其他弹出宿主，否则两个宿主会产生相互独立的捕获、焦点和关闭生命周期。
