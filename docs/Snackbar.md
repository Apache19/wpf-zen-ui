# Snackbar 使用说明

`Snackbar` 用于显示短暂、非模态的操作反馈。每个表示作用域显式放置一个
`SnackbarPresenter`；`ISnackbarService` 只向绑定的 Presenter 显示通知，不查找全局窗口，
也不依赖 `NavigationView`。

这种设计让不同窗口、`ElementHost`、`HwndSource` 和插件区域拥有相互隔离的通知目标、当前
通知和队列。本文中的示例假定已声明：

```xml
xmlns:ui="http://schemas.thezendev.com/wpf/2027"
```

## 在根容器中放置 Presenter

把 `SnackbarPresenter` 放在需要覆盖的视觉区域根面板中，并声明在普通内容之后：

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

        <!-- 窗口级瞬时 UI 不属于 NavigationView。 -->
        <ui:SnackbarPresenter
            x:Name="RootSnackbarPresenter"
            Grid.Row="0"
            Grid.RowSpan="2"
            Margin="24"
            HorizontalAlignment="Right"
            VerticalAlignment="Bottom" />
    </Grid>
</Window>
```

Presenter 的父容器和布局属性共同决定 Snackbar 的覆盖范围与位置。不要把它放进导航页面：
页面切换会卸载 Presenter、清空当前通知和等待队列；它也无法覆盖页面之外的 Shell 区域。

如果 Shell 同时使用 `ContentDialogHost`，将页面与 SnackbarPresenter 放入 Host.Content：

```xml
<ui:ContentDialogHost x:Name="RootContentDialogHost">
    <Grid>
        <local:ApplicationView />
        <ui:SnackbarPresenter x:Name="RootSnackbarPresenter"
                              HorizontalAlignment="Right"
                              VerticalAlignment="Bottom" />
    </Grid>
</ui:ContentDialogHost>
```

Host 会在模态期间禁用自己的普通内容分支，Snackbar 因而不能接受交互；只有 `TitleBar` 的拖动区域与系统窗口按钮作为窗口外壳保持可用。
应用仍应避免在需要用户立即处理模态内容时发送无关通知。

## 使用表示作用域服务

服务生命周期必须与 Presenter 所在的窗口或区域一致：

```csharp
services.AddScoped<ISnackbarService, SnackbarService>();
```

不要注册为 Singleton，否则多个窗口会争用同一个服务，而服务也会拒绝从第一个 Presenter
重定向到后续 Presenter。

Shell 在 `InitializeComponent()` 后，把当前作用域的服务绑定到 Presenter：

```csharp
public partial class MainWindow
{
    private readonly INavigationScope _scope;

    public MainWindow(INavigationScopeFactory scopes)
    {
        InitializeComponent();

        _scope = scopes.Attach(RootNavigation, "main");
        _scope.Services
            .GetRequiredService<ISnackbarService>()
            .Attach(RootSnackbarPresenter);

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

没有导航时，为窗口、`ElementHost` 或插件区域创建普通 `IServiceScope` 也可以。表示区域永久
销毁时应释放该作用域。Presenter 可以在加载前绑定，但通知只能在 Presenter 已经 `Loaded`
后、从它的 Dispatcher 线程显示。

`Attach` 再次传入同一 Presenter 是幂等操作；试图换成不同 Presenter 会抛出
`InvalidOperationException`。这种不可变绑定可以保证已经注入页面或视图模型的服务不会被
其它窗口的初始化过程重定向。

## 显示通知

引入扩展方法命名空间后，最简单的通知只需要标题和消息：

```csharp
using Wpf.Zen.Ui.Extensions;

public sealed class ExportViewModel(ISnackbarService snackbars)
{
    public void ReportCompleted(string fileName)
    {
        snackbars.Show("导出完成", $"已保存 {fileName}");
    }
}
```

可以指定语义外观、图标和显示时间：

```csharp
snackbars.Show(
    "连接失败",
    "请检查网络设置后重试。",
    ControlAppearance.Danger,
    new FluentIconSource(SymbolRegular.Warning24),
    TimeSpan.FromSeconds(8));
```

`icon` 是普通内容槽，推荐传入可安全复用的 `FluentIconSource`，而不是已经属于另一个视觉树
的 `FluentIcon` 元素。

常用 `ControlAppearance` 值包括 `Normal`、`Info`、`Success`、`Caution` 和 `Danger`。
外观只表达通知语义，不替代清晰的标题和消息。

`DefaultTimeOut` 默认为 5 秒。扩展方法未指定时间时会使用这个值；调用完整 `Show` 方法并把
`timeout` 设为 `TimeSpan.Zero` 时也会回退到 `DefaultTimeOut`：

```csharp
snackbars.DefaultTimeOut = TimeSpan.FromSeconds(4);
snackbars.Show("草稿已保存", "所有更改均已同步。");
```

服务会立即替换当前通知，然后从新通知的 `Timeout` 重新计时。它适合“最新状态优先”的应用
反馈，不会为每次服务调用保留一个等待队列。

## 直接创建 Snackbar 与队列

调用方需要订阅事件、设置模板或按顺序显示多个通知时，可以绕过服务，直接创建
`Snackbar`：

```csharp
var snackbar = new Snackbar(RootSnackbarPresenter)
{
    Title = "正在同步",
    Content = "剩余 3 个文件",
    Appearance = ControlAppearance.Info,
    Icon = new FluentIconSource(SymbolRegular.ArrowSync24),
    Timeout = TimeSpan.FromSeconds(4),
    IsCloseButtonEnabled = true,
};

snackbar.Closed += (_, _) => viewModel.OnNotificationClosed();
snackbar.Show();
```

`Show()` 和 `ShowAsync()` 默认把实例加入 Presenter 队列；队首通知计时结束并完成关闭动画后，
才显示下一项。`Show(immediately: true)` 会先隐藏当前通知，再立即显示新实例。

`ShowAsync()` 只表示“提交显示请求”的异步过程，并不等待 Snackbar 最终关闭。需要观察显示
生命周期时，应订阅冒泡路由事件 `Opened` 和 `Closed`。

常用属性如下：

| 属性 | 默认值 | 用途 |
| --- | --- | --- |
| `Title`、`TitleTemplate` | `null` | 标题内容及其模板 |
| `Content`、`ContentTemplate` | `null` | 主消息及其模板 |
| `Icon` | `null` | 左侧图标内容槽 |
| `Appearance` | `Normal` | 通知的语义外观 |
| `Timeout` | 2 秒 | 直接创建实例时的显示时间 |
| `IsCloseButtonEnabled` | `true` | 是否显示关闭按钮 |
| `IsShown` | `false` | 当前是否处于显示状态，通常由 Presenter 管理 |

注意，直接创建 `Snackbar` 的 `Timeout` 默认值是 2 秒，而 `SnackbarService.DefaultTimeOut`
默认值是 5 秒；它们服务于不同的调用入口。

## 多窗口与非 Window 宿主

Presenter 是服务的 owner；`Window` 不是。每个独立视觉区域都应有自己的 Presenter 和服务：

```text
Window A / ElementHost A       Window B / plug-in region B
├─ application content        ├─ application content
└─ SnackbarPresenter A        └─ SnackbarPresenter B
   └─ service A                  └─ service B
```

因此，即使 `Window.GetWindow(root)` 返回 `null`，服务仍能在 `HwndSource` 或 WinForms
`ElementHost` 中工作。一个原生窗口中存在多个插件区域时，也应给每个区域创建独立 Presenter
和 DI 作用域，以免不相关区域混合通知。

Presenter 卸载时会停止计时器、关闭当前 Snackbar 并清空队列。这可以避免已经脱离视觉树的
区域继续保留通知实例；重新加载后不会恢复旧通知。

## 使用建议与常见错误

- Snackbar 是非模态反馈，不适合确认删除、要求用户立即选择或显示必须阅读的错误；这些场景
  应使用 `ContentDialog`。
- 标题和消息应简短，并让超时时间足以阅读。重要但无需确认的错误可以使用更长时间和
  `Danger` 外观。
- `Attach` 前调用服务：服务会报告尚未绑定 `SnackbarPresenter`。
- Presenter 尚未加载：等视觉树加载完成后再响应通知请求。
- 从后台线程显示：先通过 Presenter Dispatcher 切回 UI 线程。
- 把 Presenter 放进页面：导航后通知与队列会被清空，应改放在 Shell 或插件区域根容器。
- 把服务注册为 Singleton：改为每个窗口或区域独立的 Scoped/自管实例。
- 期望服务排队：服务采用立即替换语义；需要队列时直接创建独立 `Snackbar` 并调用 `Show()`。
