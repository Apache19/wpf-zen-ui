# ContentDialog 使用说明

`ContentDialog` 在应用自己的视觉树内呈现模态内容。它不创建新的 WPF `Window`，也不依赖
`NavigationView` 或全局窗口注册；每个表示作用域显式放置一个 `ContentDialogHost`，并把该
宿主实例交给对话框或 `IContentDialogService`。

这种模型同样适用于多窗口、一个窗口内的多个独立区域、WinForms `ElementHost`、
`HwndSource` 和插件 UI。决定对话框覆盖范围的不是原生窗口，而是宿主在元素树中的位置。

本文中的示例假定已声明 WPF Zen UI 的 XAML 命名空间：

```xml
xmlns:ui="http://schemas.thezendev.com/wpf/2027"
```

## 在根容器中放置宿主

把业务内容和 `ContentDialogHost` 放在同一个 `Grid` 中，并让宿主成为最后一个子元素。
宿主应跨越所有需要覆盖的行和列：

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

        <!-- 最后声明，以覆盖上面的窗口内容。 -->
        <ui:ContentDialogHost
            x:Name="RootContentDialogHost"
            Grid.Row="0"
            Grid.RowSpan="2"
            IsDisableSiblingsEnabled="True" />
    </Grid>
</Window>
```

不要把宿主放进 `NavigationView` 的页面内容或控件模板。否则它只能覆盖导航内容，无法覆盖
标题栏、导航面板等同属窗口的区域，页面切换或模板重建还可能中断正在显示的对话框。

`IsDisableSiblingsEnabled="True"` 会在对话框显示期间禁用宿主父容器中的同级元素，并在关闭
后恢复各元素原来的启用状态。它的作用域严格由“宿主的直接父级”决定，因此：

- 需要模态化整个 Shell 时，把宿主作为 Shell 根面板的直接子元素。
- 只需要模态化插件或局部工作区时，把宿主放在该区域的根面板中。
- 不要把宿主包在一个只有装饰用途的额外容器里；此时被禁用的只会是这个额外容器中的
  同级元素，而不是实际业务内容。
- 宿主必须和需要阻止输入的内容位于同一个 WPF 元素树中。它不会跨越 `Popup`、另一个
  `HwndSource` 或另一个原生窗口。

默认值为 `False`。只有应用通过其它机制可靠地阻止背景交互时，才应保留默认值。

## 使用表示作用域服务

使用依赖注入时，服务生命周期必须与拥有宿主的表示作用域一致。不要把服务注册为
Singleton，因为 Singleton 会让所有窗口和插件共享同一个不可变宿主。

```csharp
services.AddScoped<IContentDialogService, ContentDialogService>();
```

如果 Shell 已经拥有 `INavigationScope`，可以让对话框服务使用同一个 DI 子作用域。窗口在
`InitializeComponent()` 之后绑定宿主：

```csharp
public partial class MainWindow
{
    private readonly INavigationScope _scope;

    public MainWindow(INavigationScopeFactory scopes)
    {
        InitializeComponent();

        _scope = scopes.Attach(RootNavigation, "main");
        _scope.Services
            .GetRequiredService<IContentDialogService>()
            .Attach(RootContentDialogHost);

        DataContext = _scope.Services
            .GetRequiredService<MainWindowViewModel>();
    }

    protected override void OnClosed(EventArgs e)
    {
        _scope.Dispose();
        base.OnClosed(e);
    }
}
```

没有导航时，可为窗口、`ElementHost` 或插件区域创建普通 `IServiceScope`，从该作用域解析服务，
并在视觉区域永久销毁时释放作用域。不要把临时的 `Loaded`/`Unloaded` 当作作用域生命周期；
WPF 在换模板或重新挂接元素树时也可能触发它们。

`Attach` 是一次性绑定：再次传入同一宿主是幂等操作，尝试换成另一个宿主会抛出
`InvalidOperationException`。这可以防止较晚初始化的窗口悄悄重定向已注入其它对象的服务。

宿主可以在加载前绑定，但只能在宿主已经 `Loaded` 后显示对话框。显示操作也必须从宿主的
Dispatcher 线程发起。

## 显示简单对话框

页面或视图模型只需注入当前作用域的服务：

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

`ShowAlertAsync` 适合只有一个关闭按钮的提示：

```csharp
await dialogs.ShowAlertAsync(
    "导出完成",
    "文件已保存到目标目录。",
    "确定",
    cancellationToken);
```

结果含义如下：

| 结果 | 含义 |
| --- | --- |
| `Primary` | 用户选择主按钮 |
| `Secondary` | 用户选择次按钮 |
| `None` | 用户选择关闭按钮、调用 `Hide()` 未指定结果，或显示被取消/中断 |

取消令牌用于结束这次显示操作；当前实现会返回 `None`，不会把
`OperationCanceledException` 传播给调用方。

## 自定义内容与交互

复杂内容可以直接创建 `ContentDialog`，其 `Content` 支持任意 WPF 对象、数据模板和绑定：

```csharp
var dialog = new ContentDialog
{
    Title = "连接设置",
    Content = new ConnectionEditorViewModel(),
    ContentTemplate = (DataTemplate)resources["ConnectionEditorTemplate"],
    PrimaryButtonText = "连接",
    CloseButtonText = "取消",
    DefaultButton = ContentDialogButton.Primary,
};

ContentDialogResult result = await dialogs.ShowAsync(dialog, cancellationToken);
```

也可以派生 `ContentDialog` 并用 XAML 定义内容，之后仍通过服务显示该实例。服务会为尚未
关联宿主的对话框设置当前作用域的宿主；如果对话框已绑定到另一个宿主，则会拒绝显示。
`DialogHost` 一经赋值不可更换。

主按钮或次按钮文本为空时，对应按钮不会显示；关闭按钮由 `CloseButtonVisibility` 明确控制。
常用属性包括：

| 属性 | 用途 |
| --- | --- |
| `Title`、`Content`、`ContentTemplate` | 标题和主体内容 |
| `PrimaryButtonText`、`SecondaryButtonText`、`CloseButtonText` | 三个底部按钮的文本 |
| `PrimaryButtonIcon`、`SecondaryButtonIcon`、`CloseButtonIcon` | 按钮图标槽 |
| `IsPrimaryButtonEnabled`、`IsSecondaryButtonEnabled`、`IsCloseButtonEnabled` | 动态控制按钮可用性 |
| `PrimaryButtonAppearance`、`SecondaryButtonAppearance`、`CloseButtonAppearance` | 按钮语义外观 |
| `DefaultButton` | Enter 键触发的按钮；内容中没有可聚焦控件时也是初始焦点候选 |
| `IsFooterVisible` | 是否呈现整个底部按钮区 |

`Opened`、`ButtonClicked`、`Closing` 和 `Closed` 是冒泡路由事件。需要验证输入时，可以在
`Closing` 中取消关闭：

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Result == ContentDialogResult.Primary && !viewModel.IsValid)
    {
        e.Cancel = true;
        viewModel.ShowValidationErrors();
    }
};
```

`Closing` 只针对通过按钮或 `Hide(...)` 发起的显式关闭。宿主被卸载、内容被另一个对话框
替换等被动移除不会触发 `Closing`，但等待中的操作会以 `None` 结束，随后触发 `Closed`。

## 不使用服务

简单的局部 UI 可以直接把宿主交给对话框：

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

服务并不提供额外的全局注册能力；它只是让视图模型能够使用当前作用域已经绑定的宿主。
如果调用方天然拥有宿主引用，直接使用通常更简单。

## 多窗口与非 Window 宿主

每个独立视觉区域都应拥有自己的宿主和服务实例：

```text
Window A / ElementHost A       Window B / plug-in region B
├─ application content        ├─ application content
└─ ContentDialogHost A        └─ ContentDialogHost B
   └─ service A                  └─ service B
```

服务绑定的是 `ContentDialogHost`，而不是 `Window`。因此在 `Window.GetWindow(root)` 返回
`null` 的 `HwndSource` 或 WinForms `ElementHost` 中仍可工作。键盘焦点约束、访问键与命令
抑制也以宿主所在区域为边界，不会修改外层 WPF `Window` 的 `InputBindings` 或
`CommandBindings`。

一个原生窗口内若有多个互不相关的插件区域，也应给每个区域创建独立的根面板、宿主和 DI
作用域；不要让它们共享服务。

## 生命周期与常见错误

- `Attach` 前调用服务：服务会报告尚未绑定 `ContentDialogHost`。
- 宿主尚未加载：等 Shell 的视觉树完成加载后再响应显示命令。
- 从后台线程显示：使用宿主 Dispatcher 切回 UI 线程，再调用服务。
- 把服务注册为 Singleton：改为每窗口或每区域的 Scoped/自管实例。
- 宿主不是最后一个同级元素：后声明的内容可能盖住对话框，应调整根容器子元素顺序。
- 宿主放错父容器：`IsDisableSiblingsEnabled` 只能禁用宿主的真实同级元素。
- 同时显示多个对话框：一个宿主一次只承载一个内容；应用应等待当前 `ShowAsync` 完成，或在
  业务层串行化请求。
