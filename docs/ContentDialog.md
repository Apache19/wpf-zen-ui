# ContentDialog 开发指南

`ContentDialog` 用于在当前 WPF 界面中请求用户确认、收集少量输入或展示必须处理的信息。
它是应用视觉树中的模态层，不会创建新的 `Window`。开发者通常通过
`IContentDialogService` 显示对话框；只有在调用方已经持有宿主引用时，才需要直接调用
`ContentDialog.ShowAsync()`。

本文示例使用以下 XAML 命名空间：

```xml
xmlns:ui="http://schemas.thezendev.com/wpf/2027"
```

## 快速开始

### 1. 在 Shell 中添加宿主

每个窗口或独立 UI 区域需要一个 `ContentDialogHost`。将它放在根 `Grid` 的最后，使遮罩能够
覆盖之前声明的内容：

```xml
<Window ...>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" />
        <ui:NavigationView
            x:Name="RootNavigation"
            Grid.Row="1" />

        <ui:ContentDialogHost
            x:Name="RootContentDialogHost"
            Grid.Row="0"
            Grid.RowSpan="2"
            IsDisableSiblingsEnabled="True" />
    </Grid>
</Window>
```

`IsDisableSiblingsEnabled="True"` 会在对话框显示时禁用宿主的同级元素，并在关闭后恢复它们
原来的启用状态。窗口级对话框建议启用此选项，避免用户通过鼠标、快捷键或访问键操作背景
界面。

### 2. 注册并绑定服务

服务实例应与宿主拥有相同的生命周期。使用依赖注入时，通常按窗口或导航作用域注册为
Scoped，不要注册为 Singleton：

```csharp
services.AddScoped<IContentDialogService, ContentDialogService>();
```

在 `InitializeComponent()` 之后，将当前作用域中的服务绑定到 XAML 宿主：

```csharp
public partial class MainWindow
{
    private readonly INavigationScope _scope;

    public MainWindow(INavigationScopeFactory scopes)
    {
        InitializeComponent();

        _scope = scopes.Attach(RootNavigation, "main");

        IContentDialogService dialogs = _scope.Services
            .GetRequiredService<IContentDialogService>();
        dialogs.Attach(RootContentDialogHost);

        DataContext = _scope.Services.GetRequiredService<MainWindowViewModel>();
    }

    protected override void OnClosed(EventArgs e)
    {
        _scope.Dispose();
        base.OnClosed(e);
    }
}
```

没有使用 `INavigationScope` 时，为每个窗口或独立区域创建普通 `IServiceScope` 即可，并在该
区域永久销毁时释放它。

### 3. 从业务代码显示对话框

引入扩展方法命名空间后，视图模型可以直接注入服务：

```csharp
using Wpf.Zen.Ui.Extensions;

public sealed class EditorViewModel(IContentDialogService dialogs)
{
    public async Task<bool> ConfirmSaveAsync(CancellationToken cancellationToken)
    {
        ContentDialogResult result = await dialogs.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "保存更改？",
                Content = "关闭前是否保存当前文档？",
                PrimaryButtonText = "保存",
                SecondaryButtonText = "不保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
            },
            cancellationToken);

        return result == ContentDialogResult.Primary;
    }
}
```

必须 `await` 显示任务后再继续需要用户选择的业务流程。服务不会维护对话框队列；如果应用的
多个请求可能同时到达，应在业务层排队或串行化它们。

## 选择合适的调用方式

| 需求 | 推荐入口 |
| --- | --- |
| 仅显示消息和一个关闭按钮 | `ShowAlertAsync(...)` |
| 标题、内容和最多三个标准按钮 | `ShowSimpleDialogAsync(...)` |
| 自定义内容、按钮外观、事件或数据模板 | 创建 `ContentDialog`，调用服务的 `ShowAsync(...)` |
| 调用方已经持有 `ContentDialogHost` | 创建带宿主的 `ContentDialog`，直接调用 `ShowAsync(...)` |

### 单按钮提示

```csharp
await dialogs.ShowAlertAsync(
    "导出完成",
    "文件已保存到目标目录。",
    "确定",
    cancellationToken);
```

如果信息不要求用户立即确认，优先考虑非模态的 `Snackbar`；删除确认、未保存更改和必须处理
的错误更适合 `ContentDialog`。

### 自定义内容

`Content` 是标准 WPF 内容槽，可使用控件、视图模型、`DataTemplate` 和绑定：

```csharp
var dialog = new ContentDialog
{
    Title = "连接设置",
    Content = connectionEditorViewModel,
    ContentTemplate = (DataTemplate)resources["ConnectionEditorTemplate"],
    PrimaryButtonText = "连接",
    CloseButtonText = "取消",
    DefaultButton = ContentDialogButton.Primary,
};

ContentDialogResult result = await dialogs.ShowAsync(dialog, cancellationToken);
```

复杂表单也可以派生 `ContentDialog`，在 XAML 中定义内容和绑定，然后仍通过服务显示：

```csharp
var dialog = new ConnectionContentDialog
{
    DataContext = connectionEditorViewModel,
};

ContentDialogResult result = await dialogs.ShowAsync(dialog, cancellationToken);
```

服务会在首次显示时为对话框关联当前宿主。`DialogHost` 一旦赋值便不能更换，因此不要把同一
对话框实例复用于不同窗口或区域；通常应为每次业务交互创建一个新实例。

## 处理用户选择

`ShowAsync()` 的返回值描述对话框如何结束：

| 返回值 | 触发方式 |
| --- | --- |
| `Primary` | 用户激活主按钮，或代码调用 `Hide(ContentDialogResult.Primary)` |
| `Secondary` | 用户激活次按钮，或代码调用 `Hide(ContentDialogResult.Secondary)` |
| `None` | 用户激活关闭按钮、调用无参数 `Hide()`、取消显示，或宿主被卸载/内容被替换 |

关闭按钮没有单独的 `ContentDialogResult`，应把 `None` 当作“未确认”分支处理。不要只判断
`Secondary` 来推断用户拒绝了操作：

```csharp
if (await dialogs.ShowAsync(dialog, cancellationToken) != ContentDialogResult.Primary)
{
    return;
}

await SaveAsync(cancellationToken);
```

传入的 `CancellationToken` 用于终止等待和移除对话框。取消会返回 `None`，不会向调用方传播
`OperationCanceledException`。如果令牌在调用前已经取消，对话框不会显示；如果显示期间宿主
被卸载（例如窗口关闭），等待任务同样以 `None` 完成。

## 按钮、默认操作与尺寸

常用属性如下：

| 属性 | 用途 |
| --- | --- |
| `Title`、`TitleTemplate` | 标题内容及模板 |
| `Content`、`ContentTemplate` | 主体内容及模板 |
| `PrimaryButtonText`、`SecondaryButtonText`、`CloseButtonText` | 三个底部按钮的文本 |
| `PrimaryButtonIcon`、`SecondaryButtonIcon`、`CloseButtonIcon` | 三个按钮的图标内容槽 |
| `IsPrimaryButtonEnabled`、`IsSecondaryButtonEnabled`、`IsCloseButtonEnabled` | 动态控制按钮是否可用 |
| `PrimaryButtonAppearance`、`SecondaryButtonAppearance`、`CloseButtonAppearance` | 设置按钮的语义外观 |
| `CloseButtonVisibility` | 显式控制关闭按钮可见性 |
| `DefaultButton` | 指定 Enter 键触发的按钮，并参与初始焦点选择 |
| `IsFooterVisible` | 显示或隐藏整个底部按钮区 |
| `DialogMaxWidth`、`DialogMaxHeight`、`DialogMargin` | 限制对话框面板的尺寸和外边距 |

主按钮和次按钮的文本为空时，对应按钮不会显示。关闭按钮的显示由
`CloseButtonVisibility` 控制。`DefaultButton` 应指向一个实际可见且可用的按钮；确认类操作
通常使用 `Primary`，破坏性操作则应避免让 Enter 键轻易触发。

`DialogMaxWidth` 和 `DialogMaxHeight` 是上限。宿主空间不足时，对话框会进一步收缩，但不会
为了适应宿主而改写开发者配置的最大值。主体内容较长时，应自行提供 `ScrollViewer` 或适当的
内容模板。

## 验证输入与关闭事件

对话框公开以下冒泡路由事件：

| 事件 | 时机与用途 |
| --- | --- |
| `Opened` | 对话框已加载并完成初始焦点设置 |
| `ButtonClicked` | 标准按钮被激活，可用于记录具体按钮交互 |
| `Closing` | 显式关闭前触发，可通过 `Cancel` 阻止关闭 |
| `Closed` | 显示生命周期结束，宿主内容已清理 |

需要在用户确认前验证表单时，在 `Closing` 中取消主按钮导致的关闭：

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Result == ContentDialogResult.Primary && !viewModel.Validate())
    {
        e.Cancel = true;
        viewModel.ShowValidationErrors();
    }
};
```

`ButtonClicked` 发生在 `Closing` 之前。`Closing` 只针对按钮或 `Hide(...)` 发起的显式关闭；显示
期间取消、宿主卸载或内容被替换等被动结束不会触发它，但 `ShowAsync()` 仍会完成，随后触发
`Closed`。

代码也可以主动结束当前对话框：

```csharp
dialog.Hide(ContentDialogResult.Primary);
```

同一个 `ContentDialog` 实例不能同时执行两次 `ShowAsync()`；第二次调用会抛出
`InvalidOperationException`。第一次调用完成并执行完 `Closed` 后，可以在同一宿主上再次显示
该实例，但按交互创建新实例通常更容易管理状态和事件订阅。

## 不使用依赖注入

调用方天然拥有宿主引用时，可以直接指定宿主：

```csharp
var dialog = new ContentDialog(RootContentDialogHost)
{
    Title = "删除项目？",
    Content = "此操作无法撤销。",
    PrimaryButtonText = "删除",
    CloseButtonText = "取消",
    PrimaryButtonAppearance = ControlAppearance.Danger,
};

ContentDialogResult result = await dialog.ShowAsync(cancellationToken);
```

`IContentDialogService` 的主要价值是让视图模型和其它业务对象不必持有视图中的宿主引用；它
不会提供全局宿主查找、并发显示或自动排队能力。

## 宿主范围与多窗口

`ContentDialogHost` 在元素树中的位置决定遮罩范围。需要覆盖整个 Shell 时，把它作为 Shell
根面板的直接子元素；只需要覆盖插件或局部工作区时，把它放在该区域的根面板中。

不要把窗口级宿主放进 `NavigationView` 的页面内容或控件模板，否则页面切换或模板重建可能
中断正在显示的对话框，而且遮罩无法覆盖标题栏、导航面板等 Shell 内容。

`IsDisableSiblingsEnabled` 只会禁用宿主父面板中的实际同级元素，也不会跨越 `Popup`、另一个
`HwndSource` 或另一个原生窗口。额外的装饰容器会改变这个作用域，因此宿主应与需要禁止输入
的业务内容共享直接父面板。

每个窗口、WinForms `ElementHost`、`HwndSource` 或独立插件区域应拥有自己的宿主和服务实例：

```text
Window A / ElementHost A       Window B / plug-in region B
├─ application content        ├─ application content
└─ ContentDialogHost A        └─ ContentDialogHost B
   └─ service A                  └─ service B
```

服务绑定宿主而不是 `Window`，因此同样适用于 `Window.GetWindow(root)` 返回 `null` 的 UI 树。
`Attach` 可以在宿主加载前调用；再次传入同一宿主是幂等操作，但尝试改绑其它宿主会抛出
`InvalidOperationException`。

## 常见问题

### 服务提示尚未绑定宿主

确保在 `InitializeComponent()` 之后对当前作用域的服务调用了 `Attach(...)`，并确认视图模型
解析自同一个作用域，而不是应用根容器或另一个窗口的作用域。

### 宿主尚未加载

只能在 `ContentDialogHost.Loaded` 之后显示。不要在窗口构造函数中过早调用 `ShowAsync()`；可
在 `Loaded`、导航完成或用户命令中发起显示。

### 后台线程调用失败

显示操作必须从宿主的 Dispatcher 线程发起。后台任务完成后，通过 UI Dispatcher 切回，再
调用对话框服务。

### 对话框没有覆盖完整窗口

检查宿主是否位于 Shell 根面板、是否跨越所有行列，以及是否为最后声明的子元素。若只禁用
了少量控件，检查宿主与业务内容是否真的是同一父面板的同级元素。

### 多个显示请求互相替换

一个宿主一次只能承载一个对话框，服务不负责排队。等待当前 `ShowAsync()` 完成后再显示下一
个，或在应用层使用队列/互斥机制协调请求。不要并发显示同一个对话框实例。
