# ContentDialog

`ContentDialog` 是继承 `HeaderedContentControl` 的可组合对话框。`ContentDialogHost` 包裹普通页面，拥有展示会话、覆盖层、范围内输入隔离、焦点恢复和过渡动画。它不创建原生窗口，也可用于 HwndSource 或 ElementHost 中的 WPF 区域。

## 内容与按钮

| 区域 | 属性 |
| --- | --- |
| 标题 | 继承的 `Header`、`HeaderTemplate`、`HeaderTemplateSelector`、`HeaderStringFormat` |
| 正文 | 继承的 `Content`、`ContentTemplate`、`ContentTemplateSelector`、`ContentStringFormat` |
| 页脚 | `Footer`、`FooterTemplate`、`FooterTemplateSelector`、`FooterStringFormat` |

Footer 与 Header 一样维护逻辑子节点。直接放入的元素可继承 DataContext、查找资源并参与逻辑树遍历；更换 Footer 会解除旧节点的所有权。显式 DataTemplate 优先于选择器；格式串用于 WPF 默认的文本呈现。Footer 为 null 时默认模板折叠页脚。默认模板拥有页脚背景、分隔线和 `FooterPadding`，Footer 内容只需声明内部构成。

按钮直接使用 WPF Button 或库内 Button，自行拥有 Style、Appearance、Icon、Command、CommandParameter、CommandTarget、CanExecute 和自动化属性。标准按钮区使用 `ContentDialogActionBar`：它是完整的 `ItemsControl`，既可直接添加元素，也支持 `ItemsSource`、ItemTemplate/Selector、ItemStringFormat 和 ItemContainerStyle/Selector。默认 `AdaptiveUniformGrid` 每行最多放置 3 项，间距为 8；不完整行中的项目平分整行宽度。可通过 `Columns`、`RowSpacing`、`ColumnSpacing` 和 `IsRowStretchEnabled` 调整，或替换 ItemsPanel。Footer 仍可直接放入任意元素，用于组合复选框、帮助链接、进度条或完全不同的布局。

```xml
<ui:ContentDialog x:Class="MyApp.DeleteDialog" Header="删除文件">
    <TextBlock Text="删除后无法恢复。" />
    <ui:ContentDialog.Footer>
        <ui:ContentDialogActionBar>
            <ui:Button Content="取消"
                       Command="{x:Static ui:ContentDialog.DismissCommand}" />
            <ui:Button Content="删除" Appearance="Danger"
                       Command="{x:Static ui:ContentDialog.CloseCommand}"
                       CommandParameter="{x:Static local:DeleteChoice.Delete}" />
        </ui:ContentDialogActionBar>
    </ui:ContentDialog.Footer>
</ui:ContentDialog>
```

数据驱动的按钮集合使用普通 ItemsControl 写法；数据对象决定内容和命令参数，模板中的 Button 继续拥有交互语义：

```xml
<ui:ContentDialogActionBar ItemsSource="{Binding Actions}">
    <ui:ContentDialogActionBar.ItemTemplate>
        <DataTemplate>
            <ui:Button Content="{Binding Text}"
                       Command="{x:Static ui:ContentDialog.CloseCommand}"
                       CommandParameter="{Binding Result}" />
        </DataTemplate>
    </ui:ContentDialogActionBar.ItemTemplate>
</ui:ContentDialogActionBar>
```

示例中的 `ui` 对应库的 XAML 命名空间，`local` 对应应用业务枚举所在的命名空间。

## 显示与模态范围

```xml
<ui:ContentDialogHost x:Name="DialogHost">
    <local:EditorView />
</ui:ContentDialogHost>
```

```csharp
object? result = await DialogHost.ShowAsync(new DeleteDialog(), cancellationToken);
if (result is DeleteChoice.Delete)
{
    // 处理业务选择。
}
```

在宿主已加载、可见且位于其 Dispatcher 线程时显示。Host.Content 始终是普通页面，`CurrentDialog` 是只读的当前对话框状态，包含入场和退场。宿主忙、对话框已在展示或已有父节点时会拒绝显示；等待返回任务结束后可以复用同一实例。

Host 应获得有限的布局空间，避免在遮罩方向被 StackPanel 或 ScrollViewer 无限测量。只有它包裹的 WPF 内容属于模态范围；其它区域可独立使用。原生子窗口仍受 WPF airspace 限制，覆盖层不是原生窗口的全局模态机制。

默认模板在展示期间禁用被覆盖的普通内容分支；`SetCurrentValue` 保留这些分支上的 IsEnabled 绑定与动态资源表达式。若内容树包含 `TitleBar`，Host 会保留通向标题栏的窗口外壳路径，使拖动区域和最小化、最大化、还原、关闭按钮继续工作，同时通过 `IsUserContentEnabled` 禁用标题栏中的 `Content` 与 `LeftContent`。来自对话框的键盘输入和未处理路由命令在边界停止，窗口级 InputBindings 不会在对话框处理之后继续触发；显式路由命令目标位于被覆盖区域时也会被拒绝。区域之外的独立 UI 不受影响。

`IContentDialogService` 仍可用于依赖注入，每个作用域调用一次 `Attach(DialogHost)`，随后 `ShowAsync(dialog, token)` 直接委托给 Host。它不保存 Dialog 的宿主关联，也不创建另一套展示会话。

## MVVM 调用

`IContentDialogService` 是接收控件的呈现服务。视图模型通过应用层接口提交普通数据、接收业务结果，由接口实现创建 XAML 视图并设置 DataContext，再委托给呈现服务。

Gallery 的 `IGalleryDialogService` 展示了这种分工：`ShowSaveWorkAsync(SaveWorkDialogData)` 返回 `SaveWorkChoice?`，`ShowTermsOfUseAsync(TermsOfUseDialogData)` 返回是否接受。该服务与 `IContentDialogService` 注册在同一导航作用域中。每次调用创建独立视图和数据，避免复用遗留的输入、验证或焦点状态。

```csharp
var data = new SaveWorkDialogData();
SaveWorkChoice? choice = await dialogService.ShowSaveWorkAsync(data);
// choice 表达保存、放弃或取消；data.UploadToCloud 包含用户编辑后的选项。
```

正文和页脚定义在 XAML 中，输入控件通过双向绑定更新数据，按钮通过 `CloseCommand` 返回业务枚举。视图模型不接收 DataTemplate、控件或资源键。条款校验状态由数据对象提供，验证失败后的焦点移动由视图负责。

## 关闭协议

| 入口 | 结果与策略 |
| --- | --- |
| `Close(result)` / `CloseCommand` | 返回任意业务对象或枚举；命令参数就是结果；不受 IsDismissible 限制 |
| `DismissCommand`、标题关闭、Escape、UI Automation 关闭 | IsDismissible=true 时请求关闭，结果为 null |
| `Closing` | 同步设置 Cancel=true 可否决一次普通关闭；再次请求可重新校验 |
| CancellationToken | 不触发 Closing；完成清理后任务进入 Canceled，可捕获 OperationCanceledException |
| Host 卸载、隐藏、Dispatcher 关闭或对话框确实离开可视树 | 强制释放会话与输入订阅，不可否决；尚无结果时返回 null |

第一个已接受的结果或 Token 取消胜出。Closing 内递归调用 Close 不会产生第二次请求；普通关闭被否决后仍可继续交互。Token 取消和宿主清理不能被业务校验阻止。

`Opened` 在内容已加载并完成入场后触发；入场中已关闭时不触发。`Closed` 是对话框自身的直接事件，在逻辑脱离和输入释放之后触发。其 Result 为业务结果或 null；通过 ShowAsync 的任务区分用户取消和 Token 取消。Closed 期间实例仍被保留，事件返回后才可重新显示，返回任务也在此后结束。

异步保存、提交和校验应由业务命令负责等待，成功后显式关闭：

```csharp
private async Task SaveAsync()
{
    await viewModel.SaveAsync();
    Close(viewModel.SavedItem);
}
```

`ICommand.Execute` 返回不表示异步业务完成，控件不提供执行任意命令后自动关闭的组合，也不识别异步命令、deferral 或按钮位置结果。

## 焦点、默认按钮和 Popup

初始焦点优先使用 `InitialFocus` 指定的可用对话框后代，其次保留已经在对话框内的焦点，再搜索正文的已实现内容，最后聚焦对话框自身。自动搜索会跳过 `Appearance` 为 `Danger` 或 `Caution` 的 `ui:Button`，避免初始焦点落在危险或警告操作上。`InitialFocus` 可通过 ElementName 绑定指向具体元素，并允许显式选择这些按钮；不可用或不属于对话框的目标被忽略。

宿主在禁用背景内容前保存键盘焦点，关闭时恢复。若触发按钮仍因异步命令执行中而被禁用，会在任务续体及绑定更新后再尝试一次。已移到其他控件的焦点会保留，新对话框会使上一次的延迟恢复失效。

默认操作直接设置 `Button.IsDefault`。访问键作用域与当前对话框一致，沿用 WPF 默认按钮处理；接受 Return 的多行编辑器可以先消费 Enter，不会自动转为确认。未指定或不可用的默认按钮不会隐式替换为其它按钮。Tab/Ctrl+Tab 在对话框内循环。

取消按钮推荐使用 `DismissCommand`。原生 `Button.IsCancel=true` 且 Command=null 时产生的 WPF Window 取消命令被转为当前对话框的用户取消，不会关闭外层 Window，也遵守 IsDismissible 和 Closing。Escape 采用冒泡处理，编辑器或弹出内容可先消费它。

焦点归属识别同时检查视觉树、逻辑树及 Popup/ContextMenu 的 PlacementTarget。弹出内容可获得焦点，不能将焦点移到被覆盖的页面。ContextMenu 等保留 WPF 自身的内部访问键范围和输入处理；跨弹出树的命令应按 WPF 规则显式设置 CommandTarget 指向对话框内的目标，不依赖控件转发业务命令。

## 模板与动画

`ContentDialog` 的默认模板是带明确视觉取舍的标准对话框布局，而不是任意布局生成器。Header、Content 和 Footer 决定各区域内部的内容；需要跨区域排版时替换 ControlTemplate。`HeaderPadding`、继承的 `Padding`、`FooterPadding` 分别控制三个区域的外壳间距，`CornerRadius` 控制对话框外壳圆角，默认模板会把其中两个底部圆角同步给 Footer 背景。正文默认由 PassiveScrollViewer 包裹，使对话框受到 MaxHeight 限制时仍固定标题和操作区；ScrollViewer 附加属性可配置其滚动行为。右上角关闭按钮独立贴近外壳顶部，标题使用更低的默认起始位置，并通过 HeaderPadding 的右侧空间避免重叠，不会根据任意 Header 内容与按钮做垂直对齐。

- Dialog 的 `PART_ContentPresenter` 是可选的正文 ContentPresenter，用于定位 DataTemplate 实现的初始焦点。自定义模板缺少它时搜索直接 Content，再退到 Dialog。
- Host 的 `PART_ContentRoot` 是必需的 UIElement，只能包裹普通页面。Host 在该部件下隔离被覆盖内容；标题栏窗口外壳保持启用，标题栏用户内容与其它页面分支被禁用。缺少该部件时拒绝展示。
- Host 的 `PART_DialogPresenter` 是可选的 `ContentDialogTransitionPresenter`。默认模板通过它呈现 CurrentDialog；自定义 Host 模板可以省略该部件并自行呈现 CurrentDialog，此时 Host 保留完整会话语义并即时完成过渡。
- `ContentDialogTransitionPresenter` 模板中的 `PART_DialogRoot` 与 `PART_Backdrop` 均为可选 UIElement。前者只包裹 Dialog，Presenter 在保留模板原有 RenderTransform 的前提下附加私有缩放；后者独立绘制遮罩，避免对 Dialog 叠加两次透明度。缺少其中一个只会关闭对应视觉效果，两个都缺少时过渡即时完成。
- 覆盖层应在 CurrentDialog 非 null 时保持命中测试，直到退场清理完成。Presenter 使用单一可控 AnimationClock 同步驱动遮罩透明度、Dialog 透明度与缩放；快速关闭会从当前画面反向退场，不先跳到完全打开。关闭动画或系统关闭客户区动画时直接完成过渡；Token 取消、卸载和 Dispatcher 关闭会同步移除时钟并重置，不依赖后续 UI 调度。Host 模板替换会同步新 Presenter 到当前会话状态，不重播入场。

`IsAnimationEnabled` 默认 true。`OpenDuration` 与 `CloseDuration` 默认都是 167ms，零表示即时过渡，负数被拒绝；`OpeningScale` 默认 0.94，`ClosingScale` 默认 0.97，必须是有限正数。`OpeningEasingFunction` 与 `ClosingEasingFunction` 只作用于缩放，透明度保持线性。动画不会改写 Dialog 自身的 Opacity 或 RenderTransform。

常规动画定制直接设置 Host 上的语义属性：

```xml
<ui:ContentDialogHost
    x:Name="DialogHost"
    OpenDuration="0:0:0.2"
    CloseDuration="0:0:0.12"
    OpeningScale="0.9"
    ClosingScale="0.98">
    <local:EditorView />
</ui:ContentDialogHost>
```

需要改变遮罩、阴影、旋转轴或布局结构时，为 `ContentDialogTransitionPresenter` 提供自定义模板并保留所需模板部件；无需定义 VisualState 或 Storyboard。需要逐帧协调额外的纯渲染效果时，可以派生 Presenter 并重写 `ApplyTransitionVisuals`。若自定义整个 Host 模板，应将上述过渡属性通过 TemplateBinding 转发给 `PART_DialogPresenter`，以便 Host 上的配置继续生效。

标准 Width、Height、Min/MaxWidth、Min/MaxHeight、Margin 和 Padding 控制布局。对话框自动化名称优先使用 AutomationProperties.Name，否则使用 Header 的文本；自动化暴露 Window 模式与对话框语义。
