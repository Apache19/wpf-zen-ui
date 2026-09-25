# Popup 跟随宿主

在 `Popup` 上设置 `PopupFollow.IsEnabled="True"`，可让打开的弹出层随
`PlacementTarget` 移动。默认关闭，可在运行中启用或禁用。

```xml
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <Button x:Name="Anchor" Content="定位目标" />
    <Popup PlacementTarget="{Binding ElementName=Anchor}"
           Placement="Bottom"
           ui:PopupFollow.IsEnabled="True"
           IsOpen="{Binding IsPopupOpen}">
        <Border Background="White" Padding="16">
            <TextBlock Text="随目标移动的内容" />
        </Border>
    </Popup>
</Grid>
```

也可以在代码中启用：

```csharp
PopupFollow.SetIsEnabled(popup, true);
```

## 跟随范围

- 宿主窗口移动、调整大小，以及目标所在原生窗口的位置变化。
- ScrollViewer 滚动、布局重新排列、目标尺寸变化。
- 目标或祖先的 LayoutTransform、RenderTransform 和变换动画。
- 运行中切换 PlacementTarget，包括切换到其他窗口中的目标。
- 没有显式 PlacementTarget 时，使用 WPF 的默认可视父级目标。

`Absolute`、`AbsolutePoint`、`Mouse`、`MousePoint` 保持屏幕或鼠标定位语义。
建议需要跟随元素的 Popup 使用 `Bottom`、`Top`、`Left`、`Right`、`Center`、
`Relative`、`RelativePoint` 或 `Custom`。

目标暂时脱离呈现树时暂停定位，重新连接后恢复。目标滚出视口不会自动关闭 Popup；
如果需要这种行为，由所属控件决定关闭时机。

## 与现有行为组合

跟随功能通过 WPF 内部 `Popup.Reposition` 重新执行正常定位，不修改
`HorizontalOffset`、`VerticalOffset`、`PlacementRectangle` 或自定义定位回调，
保留这些属性的绑定和动态资源。屏幕边缘避让继续由 WPF 决定。

可以与标准对齐属性、`PopupPlacementGap`、`PopupPlacementTracker`、
`PopupShadow` 和 `PopupSlideAnimator` 一起使用。跟随只刷新已打开的 Popup 的位置，
保留鼠标捕获、焦点、`StaysOpen` 和 `PopupDismissal` 的原有行为。
控件主动关闭弹出层的策略仍然有效，例如 AutoSuggestBox 的宿主移动关闭策略。

仅对设置了属性的 Popup 生效，不会全局启用库中所有弹出层，也不直接作用于
ContextMenu、ToolTip 或 Flyout；需要在其实际 Popup 上配置。

## 生命周期与实现边界

打开时开始监听布局和宿主 HWND 消息，并在渲染帧检查目标屏幕几何，覆盖不触发布局的
变换动画和宿主移动。几何不变时不重复调用定位回调。关闭、卸载或禁用时解除监听并
取消排队的更新；全局事件使用弱引用，避免保留已经废弃的 Popup。

渲染帧监听只在启用且打开期间存在，会产生每帧几何检查的开销。
实现依赖 WPF 的内部 `Reposition` 方法；如果未来框架移除此入口，将输出诊断并禁用跟随。
