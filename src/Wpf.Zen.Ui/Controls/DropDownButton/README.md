# DropDownButton

`Content` 描述按钮本身，`Flyout` 独立描述弹出内容。控件保留 WPF Button 的 Click、Command、ClickMode 和访问键机制；Click/Command 执行后切换展开状态。

## 内容与呈现契约

| Flyout 值 | 呈现方式 |
| --- | --- |
| `null` | 无弹出内容；展开请求回到 false |
| `ContextMenu` | 直接打开，保留菜单的选择、命令、键盘及关闭机制 |
| `Popup` | 直接打开，保留调用方的 Child、样式和显式属性 |
| 数据对象 | 自动建立 Popup → ContentControl → ContentPresenter，使用 WPF 数据模板机制 |
| UIElement | 默认直接呈现该元素，由自动生成的 Popup 包装；元素不能已属于其他父级，也不能包含按钮自身 |

普通内容采用标准 WPF 规则：显式 `FlyoutTemplate` 优先，其次 `FlyoutTemplateSelector`，选择器返回 null 后允许按数据类型查找隐式 DataTemplate，最后采用 WPF 默认呈现（包括 UIElement 和文本）。显式模板也可以对 UIElement 进行呈现；控件不另建一套模板优先级。

`FlyoutStringFormat` 是复合格式串，不是 Binding 的 StringFormat。只在 WPF 最终将内容渲染为文本时生效；模板内的文本格式应在模板内设置。动态改变格式时，对未指定显式模板的数据内容重新应用包装模板，以刷新 WPF 缓存的格式化文本；Popup 实例和内容对象保持不变。

以上呈现属性对原生 ContextMenu / Popup 无效。需要菜单语义时使用 ContextMenu，普通弹出面板不会自动获得菜单项选择、选项点击关闭等行为。

## 数据驱动用法

```xml
<ui:DropDownButton Content="详情"
                   Flyout="{Binding SelectedItem}"
                   IsDropDownOpen="{Binding IsDetailsOpen}">
    <ui:DropDownButton.FlyoutTemplate>
        <DataTemplate>
            <StackPanel Width="280">
                <TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
                <TextBox Text="{Binding Description}" />
            </StackPanel>
        </DataTemplate>
    </ui:DropDownButton.FlyoutTemplate>
</ui:DropDownButton>
```

也可以在按钮、父容器或 Application 的 Resources 中声明带 DataType 的隐式 DataTemplate，无须设置 FlyoutTemplate。运行期间替换同一数据类型的模板资源时，按 WPF 机制在对应 ResourceDictionary 上启用 `InvalidatesImplicitDataTemplateResources`。

```xml
<ui:DropDownButton Content="状态"
                   Flyout="{Binding StatusMessage}"
                   FlyoutStringFormat="当前状态：{0}" />

<ui:DropDownButton Content="选项">
    <ui:DropDownButton.Flyout>
        <StackPanel Width="240">
            <CheckBox Content="显示详情" IsChecked="{Binding ShowDetails}" />
        </StackPanel>
    </ui:DropDownButton.Flyout>
</ui:DropDownButton>
```

## 外观和定位

包装器和内容模板分开定制：

* `FlyoutPresenterTemplate` 是以原生 **ContentControl** 为目标的 ControlTemplate，负责边框、背景、留白和滚动布局。必须呈现 Content，通常使用 `ContentPresenter ContentSource="Content"`；该写法同时传递 ContentTemplate、ContentTemplateSelector 和 ContentStringFormat。没有命名部件要求。null 或省略内容呈现器意味着没有相应视觉内容。
* `FlyoutPopupStyle` 是以 **Popup** 为目标的 Style，负责 Placement、偏移、大小约束、StaysOpen、透明和动画。Popup 本身不是 Control，因此不使用 ControlTemplate。Child、IsOpen、PlacementTarget 由控件管理，不应在这个 Style 中设置。

包装模板资源键是 `UiDropDownButtonFlyoutPresenterTemplate`，由默认按钮样式提供。
Popup 样式使用公开的组件资源键 `{x:Static ui:DropDownButton.FlyoutPopupStyleKey}`，由生成宿主的代码建立动态资源引用，不依赖按钮 Style 或 Template 中的 Setter/Binding。可以在按钮、祖先或 Application.Resources 中使用此键配置对应作用域，资源替换和逻辑树位置变化遵循 WPF 的动态资源查找机制。

`FlyoutPopupStyle` 默认为 null，表示使用上述资源查找；非 null 时作为单个按钮的显式样式，设回 null 则恢复资源查找。Popup 同时以该组件键作为 DefaultStyleKey，因此局部样式无需 BasedOn 即可保留库的主题默认值。显式样式优先于主题样式（包括主题触发器）；需要完全禁用主题默认值时可设置 `OverridesDefaultStyle=True`。此机制只作用于自动生成的 Popup，不修改 Flyout 中直接提供的 Popup 或 ContextMenu。

例如在窗口资源中统一配置弹出偏移：

```xml
<Style x:Key="{x:Static ui:DropDownButton.FlyoutPopupStyleKey}" TargetType="Popup">
    <Setter Property="VerticalOffset" Value="8" />
</Style>
```

```xml
<ui:DropDownButton Content="详情" Flyout="{Binding SelectedItem}">
    <ui:DropDownButton.FlyoutPopupStyle>
        <Style TargetType="Popup">
            <Setter Property="Placement" Value="Right" />
            <Setter Property="HorizontalOffset" Value="8" />
            <Setter Property="MaxHeight" Value="400" />
        </Style>
    </ui:DropDownButton.FlyoutPopupStyle>
    <ui:DropDownButton.FlyoutPresenterTemplate>
        <ControlTemplate TargetType="ContentControl">
            <Border Background="White" BorderBrush="Gray" BorderThickness="1" Padding="16">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <ContentPresenter ContentSource="Content" />
                </ScrollViewer>
            </Border>
        </ControlTemplate>
    </ui:DropDownButton.FlyoutPresenterTemplate>
</ui:DropDownButton>
```

生成面板按内容确定尺寸；需要限制长内容时同时设置 Popup 的大小约束，并在包装模板中放置 ScrollViewer。ContextMenu 的默认最小宽度适配按钮；调用方显式设置的宽度/样式优先。

## 继承、生命周期和交互

生成的 Popup 和显式传入的 Popup 都是按钮的逻辑子元素，显式 Popup 必须没有其他逻辑或视觉拥有者。面板内子按钮的 Click、未处理的鼠标按键和未处理的滚轮事件在 Popup 边界停止普通冒泡，避免触发拥有者的 Click、按压状态或滚动背后的页面；Popup 内部的 ScrollViewer 仍可优先处理滚轮。StaysOpen=false 时，WPF 的子树鼠标捕获也使面板外的滚轮经过此边界；显式设置 StaysOpen=true 则保留 WPF 的非捕获行为。UI 内容继承按钮的 DataContext、FlowDirection 和资源；数据模板内部的 DataContext 则是 Flyout 数据对象。继承在关闭时仍然有效，本地设置和绑定保留优先级。Popup 跨独立窗口，ElementName 仍遵循 WPF 名称作用域，不保证任意跨模板名称查找；优先使用数据绑定。

ContextMenu 保留 WPF 内部 root Popup 的逻辑所有权，按钮不直接收养菜单。默认 PlacementTarget 指向按钮，菜单打开时通过 WPF 原生机制继承按钮的 DataContext。因此按钮位于 ListBoxItem/ListViewItem 内时，菜单及菜单项可以使用当前项的数据绑定，打开期间的数据替换和关闭后再次打开也会刷新。菜单或子项显式设置的 DataContext，以及调用方指定的其他 PlacementTarget，仍按 WPF 自身规则生效。

普通内容之间切换时保留 Popup 及展开状态。切换为 null 或原生弹出对象时关闭、解绑旧宿主并释放 UI 内容所有权；新的原生对象沿用其现有 IsOpen 状态。原生对象的显式 PlacementTarget/Placement/StaysOpen 不会被覆盖，控件提供的默认 PlacementTarget 在脱离后释放。

UIElement 和 FrameworkContentElement 的所有权在依赖属性提交前检查；拒绝已被占用的元素时，原 Flyout 与宿主内容保持一致。DataContext、绑定或关闭事件回调中再次修改 Flyout 时，先完成当前所有权转移，再处理最终请求，避免在 WPF 逻辑树回调中递归修改宿主。原生打开和关闭事件以当时实际的 IsOpen 为准，允许调用方在更早的事件处理器中改变状态。

IsDropDownOpen 默认双向绑定，内部状态变化使用 SetCurrentValue 保留绑定。首次 Loaded 前对生成面板的展开请求会等到 Loaded 后执行；按钮禁用或卸载时关闭，重新加载不会自动重新打开已关闭的面板。禁用期间从外部打开原生弹出对象也会关闭，并同步回 false。

F4、Alt+Down/Up 切换展开状态，Escape 关闭；这些快捷键不执行按钮命令。生成面板或显式 Popup 打开后，会立即按 WPF 焦点遍历规则把焦点移动到首个可聚焦子元素；生成面板中的 Tab / Ctrl+Tab 在面板内循环。程序或键盘关闭生成面板且焦点仍在其中时，活动窗口内的焦点返回按钮；外部点击保留正常焦点转移。子控件已处理的键盘事件优先。ContextMenu 继续使用 WPF 原生菜单焦点行为。

UI Automation 同时提供 Button 的 Invoke 和独立的 ExpandCollapse；无 Flyout 时不提供 ExpandCollapse 模式。

实现依据为本地 WPF 源码中的 ContentPresenter（模板选择、DataContext、格式缓存）、ContentControl（逻辑内容所有权）和 Popup（Child、逻辑父级及独立窗口）。实现未使用反射。
