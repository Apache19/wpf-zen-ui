# ContentDialog

`ContentDialog` 是当前 WPF 可视树中的模态内容面板；`ContentDialogHost` 负责遮罩、作用域隔离、焦点恢复与过渡动画。它不创建原生窗口，也适用于 HwndSource 和 ElementHost 中的独立区域。

## 设计结论

- **选择与关闭分开**：底部为可选的 Primary、Secondary、Tertiary 三个操作，右上角为独立关闭入口。第三操作返回 Tertiary，关闭及无选择的取消返回 None。三个操作不必全部出现。这是明确的库内布局契约，并不假设所有平台都必须采用同一种布局。
- **移除 Closing**：WPF Unloaded 是不可取消的通知，父级移除、窗口销毁不能由子元素阻止。Hide 接受关闭结果，Closed 只通知完成；校验放在操作执行前。
- **操作定义集中定制**：三个 ContentDialogAction 属性取代成组的文本、图标、外观和启用属性。定义是 Freezable，支持从所属对话框获得 DataContext。ContentTemplate 呈现任意数据；ButtonStyle 定制完整 Button，包括 ControlTemplate、ToolTip、自动化名称和布局。
- **保留真正的 Button 容器**：任意 DataTemplate 若直接生成整个操作区，默认动作、CanExecute、无障碍及操作身份就会依赖用户模板。当前设计分离内容模板与交互容器；需要改变页脚结构时仍可替换 Dialog.Template。
- **使用 WPF 原生布局**：采用 Width、Height、MinWidth、MinHeight、MaxWidth、MaxHeight、Margin、Padding，删除 Dialog 前缀尺寸属性及反写尺寸的 MeasureOverride。FrameworkElement.MeasureCore 已负责边距和宽高约束。
- **动画放在 Host**：Host 在退出完成前保留内容和模态状态，并处理中途卸载或换模板。Dialog 的 RenderTransform、Opacity 不被动画覆盖。遮罩独立淡入淡出，本体淡入、轻微弹性缩放入场和缩小淡出，两者共用过渡时长及完成清理；尚不提供双对话框交叉转场或队列。未来转场仍应由 Host 协调。

依赖属性及事件均以 `DP: 名称`、`EVENT: 名称` 分区，标识紧邻 CLR 包装。只接受 ContentDialogHost，已移除无法提供完整生命周期的 ContentPresenter 附加宿主行为。

## 放置宿主

```xml
<Grid>
    <Grid x:Name="ApplicationContent">
        <!-- 当前区域的应用内容 -->
    </Grid>
    <ui:ContentDialogHost x:Name="DialogHost" />
</Grid>
```

Host 应作为有限尺寸父面板的最后一个同级元素，并跨越需要遮挡的行列。不要放在沿遮罩方向无限测量的 StackPanel 或 ScrollViewer 中。模态作用域为同一父级中的 WPF 内容，不覆盖其它原生窗口。

IsDisableSiblingsEnabled 默认 true，关闭时恢复宿主管理的同级元素；路由命令和访问键也在宿主作用域内隔离。无需禁用同级控件时可设为 false，但应保留 Host 的全区域覆盖和焦点约束。

```csharp
var dialogs = new ContentDialogService();
dialogs.Attach(DialogHost); // 可在 Loaded 前绑定
// 显示必须在 Host.Loaded 后、Host 的 Dispatcher 线程上进行。
```

服务显式绑定宿主，不按 Window 做全局查找。每个展示作用域创建一个服务，服务自身不可改绑。

## 三个选择

```csharp
var dialog = new ContentDialog(DialogHost)
{
    Title = "保存修改？",
    Content = "请选择如何处理当前修改。",
    PrimaryAction = new ContentDialogAction
    {
        Content = "保存",
        Appearance = ControlAppearance.Accent,
    },
    SecondaryAction = new ContentDialogAction { Content = "不保存" },
    TertiaryAction = new ContentDialogAction { Content = "取消" },
    DefaultButton = ContentDialogButton.Primary,
};
ContentDialogResult result = await dialog.ShowAsync(cancellationToken);
```

DefaultButton 默认 None，不覆盖文本编辑器对 Enter 的处理。缺失或禁用默认操作时，不会自动选择另一个按钮。已获得焦点的按钮仍具有 WPF Button 自身的 Enter/Space 行为。

PrimaryAction=null 移除按钮；PrimaryAction.IsEnabled=false 只禁用，不隐藏。三个操作都为空时页脚折叠。窄窗口及长文本下页脚允许换行。

```csharp
var result = await dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
{
    Title = "继续？",
    Content = "当前任务尚未完成。",
    PrimaryAction = new ContentDialogAction { Content = "继续" },
    TertiaryAction = new ContentDialogAction { Content = "返回" },
});
```

ShowAlertAsync(title, message, actionButtonText) 创建一个 Primary 操作，点击返回 Primary；右上角关闭返回 None。

## 绑定、数据模板与样式

标题使用 `Title`、`TitleTemplate`、`TitleTemplateSelector`、`TitleStringFormat` 四个属性。默认模板通过 `ContentSource="Title"` 交由 WPF ContentPresenter 处理：显式模板优先于选择器；未选中模板时继续隐式模板查找和默认内容呈现。`TitleStringFormat` 用于默认文本呈现，模板自行负责格式化，UIElement 标题不应用格式串。例如 `Title="{Binding Count}" TitleStringFormat="数量：{0:N0}"`。

```xml
<ui:ContentDialog x:Name="EditorDialog" Title="编辑资料">
    <ui:ContentDialog.PrimaryAction>
        <ui:ContentDialogAction
            Content="{Binding SaveCaption}"
            Command="{Binding SaveCommand}"
            CommandParameter="{Binding CurrentItem}"
            Appearance="Accent">
            <ui:ContentDialogAction.ContentTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding}" FontWeight="SemiBold" />
                </DataTemplate>
            </ui:ContentDialogAction.ContentTemplate>
            <ui:ContentDialogAction.ButtonStyle>
                <Style TargetType="{x:Type ui:Button}">
                    <Setter Property="Padding" Value="20,8" />
                    <Setter Property="ToolTip" Value="保存当前资料" />
                    <Setter Property="AutomationProperties.Name" Value="保存资料" />
                </Style>
            </ui:ContentDialogAction.ButtonStyle>
        </ui:ContentDialogAction>
    </ui:ContentDialog.PrimaryAction>
</ui:ContentDialog>
```

依赖 DataContext 的定义应为每个对话框单独创建。Dialog 或操作内容若是 UIElement，也只能有一个父级；重复使用视图时优先使用数据对象加 DataTemplate。

ContentTemplate 的 DataContext 是操作的 Content。默认模板绑定 Content、Icon、Appearance、Command 等语义属性；ButtonStyle 补充其它属性及整个 Button.Template。样式设置器不能覆盖这些本地绑定，命令应配置在操作定义上。RoutedCommand 以 Dialog 为执行目标，普通 ICommand 收到 CommandParameter。

右上角通过 CloseButtonStyle 定制。例如设置 Content="×"、ToolTip="关闭"、AutomationProperties.Name="关闭对话框"；默认关闭名称为英文，应用可按语言替换样式。

控件在 OnApplyTemplate 中为四个命名按钮部件建立样式绑定。自定义 Dialog.Template 无需重复编写 Style 绑定；替换操作定义、修改 ButtonStyle 或 CloseButtonStyle 均会更新部件。非空样式覆盖部件样式；null 或缺失操作使用模板应用时的部件默认样式，因此清空 CloseButtonStyle 后仍保留默认关闭图标和自动化名称。

## 校验与异步操作

调用顺序为：检查 IsEnabled/CanExecute → ButtonClicked → 再次检查当前操作与会话 → Command → 根据 ClosesDialog 关闭。

```csharp
dialog.ButtonClicked += (_, e) =>
{
    if (e.Button == ContentDialogButton.Primary && !IsValid())
    {
        e.Cancel = true;
        ShowValidationMessage();
    }
};
```

Cancel 只取消当前操作及命令，不阻止 Hide、CancellationToken 或父级移除。Handled 只有普通路由事件语义，不等价于 Cancel。

异步验证设 ClosesDialog=false。ICommand.Execute 返回 void，控件不猜测异步命令何时完成：

```csharp
dialog.PrimaryAction = new ContentDialogAction
{
    Content = "保存",
    Command = SaveCommand,
    ClosesDialog = false,
};
// SaveCommand 的异步实现中：
// 禁用操作或通过 CanExecute 表示忙碌，await 保存并处理异常。
// 成功后回到 UI Dispatcher 调用 dialog.Hide(ContentDialogResult.Primary)。
// 用户取消或卸载时，业务操作应使用自己的 CancellationToken 停止。
```

IsDismissible=false 隐藏右上角关闭，禁止 Escape 和 UI Automation 关闭，但不阻止 Hide、取消或卸载。未保存数据必须由业务生命周期管理，不能依赖子控件否决窗口销毁。

## 生命周期与并发

- ShowAsync 只在宿主加载后调用。宿主占用、实例重复显示或已有父级会明确失败。
- IsOpen 表示会话尚未释放，包括入场、退出和 Closed 通知期间。
- Opened 每次显示最多一次，在内容加载及入场完成后触发。入场期间关闭则不触发。
- Hide(result) 接受第一个结果，重复调用不改变结果。IsClosing 表示已接受关闭、尚未完成清理。
- 显式关闭等退出动画完成后才移除内容、释放模态状态并触发 Closed。await ShowAsync 在清理后返回。
- CancellationToken（包括显示前已取消）返回 None，不抛出 OperationCanceledException。后台取消只完成线程安全的任务源，WPF 操作仍在 UI Dispatcher 执行。
- 父级移除 Host、关闭窗口、隐藏宿主、模板不再呈现对话框或外部替换 Content 都会终止未完成显示。外部替换不等退出动画；旧会话不会清空新内容。
- 第一个接受的结果保持不变；接受选择后发生卸载不会改写选择。
- Closed 是直接通知，应订阅在 Dialog 自身；父级已被移除，不再冒泡。Closed 中仍不能重入同一实例；等待 ShowAsync 返回再复用。连续显示串行 await，库不自动排队。
- 会话外可以更换 DialogHost；服务自身仍遵循一次 Attach 的作用域约束。

## 布局、焦点及模板契约

Dialog 默认 MaxWidth=640、MaxHeight=850、Margin=24，可用标准属性覆盖。内容区在有限空间内滚动，标题和页脚保留布局空间。显式 Width、MinWidth 等遵守 WPF 自身优先级；超出宿主的硬性最小尺寸可能被裁切，不会静默改写用户配置。

初始焦点优先进入已实现的内容（包括 DataTemplate 中的输入控件），其次是明确默认按钮或安全可用按钮，最后是对话框自身。危险/警告操作不自动获得初始焦点。Tab 循环，Escape 冒泡处理，让编辑器或 Popup 先消费按键。

自定义 Dialog.Template：

- PART_ContentPresenter（可选 ContentPresenter）帮助查找内容模板中的输入控件。
- PART_PrimaryButton、PART_SecondaryButton、PART_TertiaryButton、PART_CloseButton 是可选 ButtonBase 部件，控件负责其 Style 绑定，并用可聚焦的 Button 辅助初始焦点。模板可直接设置部件默认样式，供未指定样式覆盖时回退。
- 标题 ContentPresenter 可用 ContentSource="Title" 自动转发内容、模板、模板选择器和格式串。
- 操作按钮使用 ContentDialog.InvokeActionCommand，参数为对应 ContentDialogButton。
- 关闭按钮使用 ContentDialog.CloseCommand。不要依赖无 Command 的 IsCancel，避免触发外层 Window 取消命令。
- 按钮必须位于 Dialog 命令路由内；部件缺失时程序显示和关闭仍可工作。

自定义 Host.Template：

- PART_TransitionRoot 是可选 FrameworkElement，仅包装对话框内容。
- Host 独占该部件的 Opacity、RenderTransform、RenderTransformOrigin；Dialog 自身属性不被覆盖。
- PART_Backdrop 是可选 FrameworkElement，作为本体包装层的同级元素覆盖宿主区域；Host 独占其 Opacity。不要让它包含对话框，否则两层透明度会叠乘。
- 每个存在的部件独立参与同一过渡；两者均缺失时即时完成。退出动画完成前保留内容及模态隔离，模板根元素应保持可命中（默认使用透明背景），避免淡出期间输入穿透。
- 卸载、不可见、换模板、关闭动画设置会同时清除两层动画并释放等待任务。入场期间关闭从当前透明度继续淡出。
- IsAnimationEnabled 默认 true，且尊重 SystemParameters.ClientAreaAnimation；TransitionDuration 默认 220ms，零表示即时完成。

UI Automation 使用 ContentDialog 类名与 Window 模式，优先使用 AutomationProperties.Name，其次标题；不声明未实现的 ScrollItem 模式。

## 验证依据

关键框架行为已对照本地 WPF 源码：

- FrameworkElement.MeasureCore：边距及原生宽高约束。
- FrameworkElement.UnloadedEvent：不可取消的直接路由通知。
- Button.OnClick：无 Command 的 IsCancel 可调用 Window.DialogCancelCommand。
- Button.UpdateIsDefaulted：默认按钮遵循作用域及 AcceptsReturn。
- Freezable.AddInheritanceContext：非可视定义的绑定上下文。
- CalendarItem.PopulateGrids、DatePicker.InitializeCalendar：在代码中建立部件样式绑定。
- ContentPresenter.ContentSource：自动关联内容、模板、模板选择器和格式串，由 WPF 完成呈现与动态更新。

回归测试覆盖三个操作结果、独立关闭、命令可执行性、绑定、模板和样式、显式异步完成、重复请求、外部替换、后台取消、尺寸、焦点、动画中断/完成、复用及无 WPF Window 的 HwndSource。
