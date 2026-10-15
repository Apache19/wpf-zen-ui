# 导航服务使用说明

WPF Zen UI 的导航以“一个导航宿主对应一个导航作用域”为基本原则。每个
`NavigationView` 都拥有独立的导航服务、页面实例、缓存、历史和生命周期，多个窗口、
多个 Tab 或多个 `ElementHost` 之间不会因注册顺序而互相覆盖。

`NavigationView` 实现了 `INavigationHost`。应用应为每个长期存在的导航宿主创建一个
`INavigationScope`，并在宿主永久关闭时释放它。不要把 `Loaded`/`Unloaded` 当作作用域
生命周期，因为 WPF 在换模板或重新挂接视觉树时也可能暂时触发这些事件。

## 导航、历史与面包屑的责任边界

新导航模型把几个容易混在一起的概念彻底分开：

| 概念 | 职责 | 不负责 |
| --- | --- | --- |
| `MenuItemsSource` | 提供便捷入口，可自由排序、分组和过滤 | 定义页面的语义层级 |
| `NavigationMap` | 描述页面在应用中的默认语义位置 | 控制菜单布局 |
| Journal | 记录真实发生过的导航时间序列，支持前进和后退 | 推断父子关系 |
| `Breadcrumbs` | 投影当前的语义路径 | 担任页面标题或隐式导航栈 |

例如，用户从“收藏”直接打开一篇文章，Journal 会记录“收藏 → 文章”；
但面包屑仍可以是“知识库 → WPF → 文章标题”。这两者表达的是不同事实。

从自动建议框、面包屑、导航地图或导航服务打开页面时，菜单会同步当前入口的选中状态，
并将其标题行滚入可视范围。若入口位于折叠分支中，则保留展开状态，高亮并滚动到最近的
可见祖先；不会自动展开菜单、打开子菜单或转移键盘焦点。此行为也适用于尚未生成子项
容器的 `INavigationItem.Children` 数据层级。分支高亮依据菜单层级，独立于面包屑层级。

已注册菜单项的激活状态由导航条目统一维护，容器负责呈现。公开的
`NavigationViewItem.Activate/Deactivate` 会同步条目与祖先分支，但不会打开页面、修改
`SelectedItem` 或导航历史。临时卸载导航视图会关闭弹出菜单，并保留仍由其持有的容器关联；
生成容器被释放或替换时不会改变条目激活状态，因此重新挂载和容器重建都能保持一致。

## 快速开始：建立导航地图

`NavigationMap` 不是必需的；没有地图时，代码打开的匿名页面仍可正常导航，
只会产生单个面包屑节点。应用需要稳定的默认层级时，再创建地图：

```csharp
private static NavigationMap CreateNavigationMap()
{
    var map = new NavigationMap();

    map.Update(routes =>
    {
        // 结构节点没有 Target，会显示但不可点击。
        routes.Add("knowledge", "知识库");
        routes.Add<ArticleListPage>("knowledge.articles", "文章", "knowledge");
        routes.Add<SettingsPage>("settings", "设置");
    });

    return map;
}
```

在创建导航作用域后，将地图交给宿主：

```csharp
_navigationScope = navigationScopes.Attach(RootNavigation, "main");
RootNavigation.NavigationMap = CreateNavigationMap();
```

地图也可以直接在 XAML 中声明。`NavigationMap` 本身是节点集合，
`NavigationMapNode.PageType` 是 `Target` 的 XAML 友好简写；不写 `PageType` 的节点就是
只负责组织层级、不可点击的结构节点：

```xml
<ui:NavigationView x:Name="RootNavigation">
    <ui:NavigationView.NavigationMap>
        <ui:NavigationMap>
            <ui:NavigationMapNode
                Key="knowledge"
                Content="知识库" />
            <ui:NavigationMapNode
                Key="knowledge.articles"
                ParentKey="knowledge"
                Content="文章"
                PageType="{x:Type pages:ArticleListPage}" />
            <ui:NavigationMapNode
                Key="settings"
                Content="设置"
                PageType="{x:Type pages:SettingsPage}" />
        </ui:NavigationMap>
    </ui:NavigationView.NavigationMap>
</ui:NavigationView>
```

也可以把地图放进窗口或应用资源，再通过 `{StaticResource ApplicationNavigationMap}`
赋给多个 Shell。共享地图的变更会分别通知所有正在使用它的导航宿主。节点已加入地图后，
`Key` 会被锁定；需要重命名时应先移除再重新加入，以免父键和路由身份在更新途中失去一致性。

`NavigationMapNode.Key` 是稳定的语义路由标识，`ParentKey` 只表示语义上级。
推荐使用如 `knowledge.articles` 这样可读且稳定的键，不要使用可能被翻译或
修改的显示文本。

### 一个页面可以有多个位置

CLR 页面类型不等于语义位置。同一个 `SearchPage` 可以同时放在“全局搜索”和
“项目搜索”下，只要为两个节点使用不同的键。导航时用路由键明确选择位置：

```csharp
navigation.Navigate(new NavigationTarget(
    typeof(SearchPage),
    routeKey: "projects.current.search",
    parameter: projectId));
```

只按页面类型导航时，地图会选择第一个匹配节点。因此，当同一页面存在
多个语义位置时，应优先按路由键导航。

### 不完整地图和循环

地图允许父节点暂时缺失。如果 D 的可解析链只是 `B → C → D`，面包屑就显示
这个后缀，不会因为更高层的 A 缺失而放弃整条路径。运行时的循环父链也会被
截断，不会卡死 UI。不过，循环仍属于配置错误，应在业务层修正。

## 将面包屑栏与导航视图解耦

`NavigationView` 不再拥有某个 `BreadcrumbBar`。它只暴露稳定的可观察数据集
`Breadcrumbs`，面包屑栏可放在窗口标题区、页面顶部或任意其他位置：

```xml
<ui:NavigationView x:Name="RootNavigation">
    <ui:NavigationView.Header>
        <ui:BreadcrumbBar
            ItemsSource="{Binding ElementName=RootNavigation, Path=Breadcrumbs}"
            ItemClicked="BreadcrumbBar_OnItemClicked" />
    </ui:NavigationView.Header>
</ui:NavigationView>
```

点击行为也由表示层明确接入当前作用域，因此不会隐式控制其他窗口的导航：

```csharp
private void BreadcrumbBar_OnItemClicked(
    BreadcrumbBar sender,
    BreadcrumbBarItemClickedEventArgs e)
{
    if (e.Item is BreadcrumbSegment segment)
    {
        // 结构节点会返回 Rejected，无需在 Shell 中重复检查 Target。
        _ = _navigationScope.Navigation.Navigate(segment);
    }
}
```

`BreadcrumbSegment.Target` 为 `null` 时，该节点只是结构标识，不可导航。表示层
可根据 `IsNavigable` 自定义样式。

### 隐藏路径中的特定节点

某些页面需要留在导航地图中，却不适合成为面包屑节点。例如分类首页本身需要导航目标，
它也要继续作为子页面的语义父级，但界面只希望展示具体的子页面。此时将地图节点的
`IsBreadcrumbVisible` 设为 `false`：

```csharp
string categoryKey = "samples.basic-input";
map.Add<BasicInputPage>(
    categoryKey,
    "Basic Input",
    isBreadcrumbVisible: false);
map.Add<ButtonPage>("samples.button", "Button", categoryKey);
```

该属性只过滤 `Breadcrumbs` 投影，不会把节点从 `NavigationMap` 删除，也不会切断父链。
导航到 `BasicInputPage` 时路径为空；导航到 `ButtonPage` 时解析仍会穿过分类节点，但最终
只投影 `Button`。默认值为 `true`，所以只有一节的“全部示例”无需任何特例设置，仍会正常
显示。

导航地图在 XAML 中声明时也可直接使用：

```xml
<ui:NavigationMapNode
    Key="samples.basic-input"
    Content="Basic Input"
    IsBreadcrumbVisible="False"
    PageType="{x:Type pages:BasicInputPage}" />
```

在视图模型或其他非可视对象中，也可通过作用域服务获取相同状态：

```csharp
INavigationState state = navigation.State;
ReadOnlyObservableCollection<BreadcrumbSegment> items = state.Breadcrumbs;
NavigationLocation? current = state.CurrentLocation;
```

`Breadcrumbs` 集合实例保持稳定，每次导航或地图更新只发出一次 Reset 通知，适合直接
用作 WPF 绑定源，也不会把清空后逐项加入的中间状态暴露给界面。`CurrentLocationChanged`
则适合需要主动响应完整位置变化的代码。

## 为一次导航提供路径

导航地图是默认结构，并不限制个别场景。`NavigationRequest` 可以只覆盖当前一次
导航的叶节点、父级或完整路径。

### 动态叶节点标题

```csharp
var request = NavigationRequest
    .To<ArticlePage>(articleId)
    .WithBreadcrumbContent(article.Title);

navigation.Navigate(request);
```

### 为匿名页面声明父级

页面自身无需在地图中；只要父级能由地图解析，就会在父级路径后附加
当前叶节点：

```csharp
var request = NavigationRequest
    .To<ImportPreviewPage>(preview)
    .WithBreadcrumbParent(
        NavigationTarget.ForRoute("projects.current.imports"))
    .WithBreadcrumbContent("导入预览");

navigation.Navigate(request);
```

### 声明完整路径

完整路径适合无法或不应由地图推导的临时场景，也允许故意跳过某些中间级：

```csharp
var request = NavigationRequest
    .To<ReviewPage>(reviewId)
    .WithBreadcrumbs(
        new BreadcrumbSegment(
            "home",
            "首页",
            NavigationTarget.ForRoute("home")),
        new BreadcrumbSegment(
            "reviews",
            "审批",
            NavigationTarget.ForRoute("reviews")),
        new BreadcrumbSegment(
            $"review:{reviewId}",
            reviewTitle,
            NavigationTarget.ForPage<ReviewPage>(reviewId)));

navigation.Navigate(request);
```

显式路径会原样使用，因此调用方需要自己保证节点顺序、键的稳定性以及
`Target` 参数的正确性。

## 由页面或视图模型动态描述位置

当面包屑内容依赖页面加载后的数据，或父级可以在页面存活期间改变时，
让页面或它的 `DataContext` 实现 `INavigationLocationSource`：

```csharp
public sealed class ArticleViewModel : INavigationLocationSource
{
    private string _title = "正在加载…";
    private string _categoryRoute = "knowledge.articles";

    public event EventHandler? LocationChanged;

    public NavigationLocationDescriptor GetNavigationLocation(
        NavigationLocationContext context) =>
        new(
            content: _title,
            parent: NavigationTarget.ForRoute(_categoryRoute));

    private void UpdateArticle(string title, string categoryRoute)
    {
        _title = title;
        _categoryRoute = categoryRoute;
        LocationChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

宿主优先检查页面实例，其次检查它的 `DataContext`。在 `DataContext` 于导航完成后
才被设置的场景中，宿主也会重新发现位置源。当动态标题、父级或完整路径
变化时，必须触发 `LocationChanged`；这只会重新解析当前位置，不会新增 Journal 记录
或重建页面。

如果只需要把页面上的绑定值用作叶节点文本，可以使用更轻量的附加属性：

```xml
<Page
    ...
    ui:NavigationView.BreadcrumbContent="{Binding Article.Title}">
```

`BreadcrumbContent` 只修改当前面包屑的叶节点，不是页面标题，也不会修改菜单项文本。

### 在页面 XAML 中声明父节点

匿名页或临时页只需要接到地图中的某个位置时，可以直接声明父路由。属性值支持绑定，
因此页面在运行时改变归属时，当前面包屑会重新解析，但不会增加 Journal 记录：

```xml
<Page
    ...
    ui:NavigationView.BreadcrumbParentKey="projects.current.imports"
    ui:NavigationView.BreadcrumbContent="导入预览" />
```

这里声明的是稳定的地图路由键，而不是父页面类型。这样同一个父页面类型出现在地图多个
位置时不会产生歧义。请求的 `WithBreadcrumbParent(...)` 和
`INavigationLocationSource.Parent` 仍具有更高优先级，适合覆盖这份页面默认声明。

### 在页面 XAML 中声明完整路径

确实需要页面拥有一条特殊完整路径时，可挂接 `BreadcrumbPath`。每一节都支持 WPF
Binding；同时声明 `RouteKey` 或 `PageType` 的节点可点击，两者都不声明的节点仅作展示：

```xml
<Page ...>
    <ui:NavigationView.BreadcrumbPath>
        <ui:BreadcrumbPath>
            <ui:BreadcrumbPathSegment
                Key="home"
                Content="首页"
                RouteKey="home" />
            <ui:BreadcrumbPathSegment
                Key="articles"
                Content="文章"
                RouteKey="knowledge.articles" />
            <ui:BreadcrumbPathSegment
                Key="current-article"
                Content="{Binding Article.Title}"
                PageType="{x:Type pages:ArticlePage}"
                RouteKey="knowledge.article"
                Parameter="{Binding Article.Id}" />
        </ui:BreadcrumbPath>
    </ui:NavigationView.BreadcrumbPath>
</Page>
```

`BreadcrumbPath` 使用 WPF `Freezable` 的继承上下文，使集合内的非可视节点仍能继承页面的
`DataContext`。标题、路由或参数绑定变化后，活动位置会自动刷新。路径必须至少包含一节，
且每一节必须有非空 `Key`。这是一份完整路径，不会再与地图推导结果拼接。

包含绑定的路径通常应直接声明在页面上。若放进 `ResourceDictionary` 复用，请使用
`x:Shared="False"` 让每个页面取得独立实例；同一个 `Freezable` 同时属于多个页面时，WPF
无法为它选择唯一的继承上下文。没有绑定、仅包含常量的路径可以安全共享。

如果路径需要异步数据组合、条件分支或业务事件驱动，优先实现
`INavigationLocationSource`；附加属性适合纯 XAML 和简单绑定，不应承载复杂业务规则。

### 根据参数计算地图节点内容

对于模板化的动态路由，地图节点也可声明 `ContentResolver`：

```csharp
var articleNode = new NavigationMapNode(
    "knowledge.article",
    "文章",
    new NavigationTarget(typeof(ArticlePage), "knowledge.article"),
    parentKey: "knowledge.articles")
{
    ContentResolver = context =>
        context.Target.Parameter is Article article
            ? article.Title
            : "文章"
};
map.Add(articleNode);
```

`ContentResolver` 只在解析当前路径时运行。如果它依赖导航参数之外的可变状态，
状态变化后应修改节点属性以触发地图刷新，或改用 `INavigationLocationSource`
并触发 `LocationChanged`。

## 运行时重排层级

`NavigationMapNode` 是可变的。例如用户把页面拖到新分组后，只需要更新父键：

```csharp
if (map.TryGetNode(pageRoute, out NavigationMapNode node))
{
    node.ParentKey = newParentRoute;
}
```

当前页面的路径会立即重新解析，但不会产生新导航、改变 Journal 或重建页面。
`Content`、`Target` 和 `ContentResolver` 变化也具有相同的自动刷新行为。

一次操作需要修改多个节点时，使用 `Update` 让活动页面只解析最终结果：

```csharp
map.Update(routes =>
{
    if (routes.TryGetNode(firstRoute, out NavigationMapNode first)
        && routes.TryGetNode(secondRoute, out NavigationMapNode second))
    {
        first.ParentKey = newParentRoute;
        second.ParentKey = newParentRoute;
    }
});
```

`Update` 支持嵌套，即使委托抛出异常也会恢复通知状态；只要已经发生过修改，退出最外层
更新时就会通知宿主重新解析一次。它批处理的是语义位置刷新，`Nodes` 集合本身仍遵循
`ObservableCollection` 的逐项通知规则。

`Remove` 只删除指定节点，不会级联删除子节点；子节点会暂时以自己为可解析路径的
起点。这个设计使拖拽、分步加载和后台同步期间的短暂不完整状态仍然可用。

## 路径解析优先级

当多个来源同时提供位置信息时，宿主按以下顺序解析：

1. `NavigationRequest.WithBreadcrumbs(...)` 提供的完整路径。
2. 页面或 `DataContext` 的 `INavigationLocationSource` 提供的完整路径。
3. 页面 XAML 的 `NavigationView.BreadcrumbPath` 完整路径。
4. 请求声明的父级；没有时依次使用页面位置源与页面 XAML 声明的父级。
5. `NavigationMap` 中当前目标的默认链。
6. 无法解析时仅显示当前叶节点。

叶节点内容的优先级为：请求的 `BreadcrumbContent` → 页面位置源的
`Content` → 页面根节点的 `NavigationView.BreadcrumbContent` → 地图或导航入口的默认内容。

注意：菜单嵌套和上一个 Journal 项永远不参与层级推断。如果导航到一个没有地图、
父级或显式路径的匿名页面，显示单个叶节点是预期行为，而不是信息丢失。

## 从旧面包屑模型迁移

本次重构有意不保留旧的隐式层级行为：

- 删除 `NavigationView.BreadcrumbBar`；改为将任意 `BreadcrumbBar.ItemsSource` 绑定到
  `NavigationView.Breadcrumbs` 或 `INavigationService.State.Breadcrumbs`。
- 删除 `NavigateWithHierarchy` 及层级导航模式；默认层级放入 `NavigationMap`，
  单次例外使用 `NavigationRequest`。
- 删除用 Journal 或菜单 `Items` 猜测面包屑的行为。
- 旧的页面标题附加语义改为 `NavigationView.BreadcrumbContent`，它仅负责面包屑叶节点。

迁移时先把“应用稳定结构”录入地图，再把文章标题、搜索条件、拖拽归属等
运行时信息分别放入 `NavigationRequest`、`INavigationLocationSource` 或可变地图节点。

## 前进与后退

Journal 保存完整的导航 occurrence，包括参数和单次面包屑声明。前进、后退恢复的是该次
导航，而不是重新从菜单猜测一个页面：

```csharp
if (navigation.CanGoBack)
{
    navigation.GoBack();
}

if (navigation.CanGoForward)
{
    navigation.GoForward();
}
```

在后退后发起一条新的普通导航会清空前进历史。`ClearHistory()` 同时清空两个方向，
但保留当前页作为后续导航的历史起点。跨线程调用时使用 `GoBackAsync()` 和
`GoForwardAsync()`。

## 请求结果与完成边界

`Navigate`、`GoBack`、`GoForward` 返回的 `NavigationResult` 可能仍为 `Accepted`。
只有最终结果的 `Succeeded` 才表示 Frame 已提交；此时页面异步初始化可能仍在进行。
控件上保留的 bool 便捷方法仅表示受理。需要处理结果时，使用作用域服务：

```csharp
var request = NavigationRequest.To<DetailsPage>(customerId);
NavigationResult result = await navigation.NavigateAsync(request, cancellationToken);
if (result.Failure is { } failure)
{
    // 此处记录或展示失败；WasCommitted 区分提交前失败和提交后的投影通知失败。
    logger.LogError(failure, "导航失败，Frame 已提交：{Committed}", result.WasCommitted);
}
```

同步提交也可以保留句柄，然后 `await submitted.Completion`。结果包括 `Rejected`、
`TargetNotFound`、`HostNotFound`、`Cancelled`、`Failed`、`Succeeded`。
`Superseded` 供采用替代策略的宿主报告；本库 NavigationView 按提交顺序处理已受理请求，
回调里的新请求排队，不会嵌套修改历史，也不会等待前一页的初始化才允许离页。
取消令牌只取消尚未提交的请求，不撤销已显示的页面。

Frame 取消、停止或提交前失败不会改动旧位置、历史、选择或上下文。提交后通知失败时，
结果保留异常和提交事实；位置扩展无法解析时发布目标及页面明确、路径为空的位置。
NavigationView 内部 Frame 不作为独立公开导航入口，应用通过服务或控件请求导航。
`ClearHistory` 也通过请求队列执行，避免后退准备中的索引被回调删除。

## 使用依赖注入

```csharp
services.AddNavigation();
services.AddSingleton<MainWindow>();
services.AddScoped<MainWindowViewModel>();
services.AddTransient<DashboardViewModel>();
```

窗口初始化控件后挂接一个独立作用域：

```csharp
_navigationScope = navigationScopes.Attach(RootNavigation, "main");
DataContext = _navigationScope.Services.GetRequiredService<MainWindowViewModel>();
```

页面类型无需注册。注册页面由子容器解析和最终释放；未注册页面使用
ActivatorUtilities 创建，页面本身由导航租约释放，其注册的构造函数依赖仍由子容器释放。
缓存淘汰不等于 DI 已注册 transient 立即销毁：容器跟踪的对象直到子容器释放才销毁。
需要按页销毁的页面和模型，应使用显式拥有租约或拥有独立子容器的工厂。

`INavigationService` 的会话身份不可替换。旧作用域请求停止后，旧服务永久失效；
即使相同 NavigationView 已挂接新作用域，旧服务和旧排队请求也不能操作新会话。
`INavigationRouter` 只按 Attach 时的显式 id 查找，停止时立即撤销该路由。
新会话不会等待旧会话的工作；旧会话自己的 Completion 仍包含其全部收尾。

## 正常关闭与受限关闭

会话状态是 `Attaching → Attached → Stopping → Draining → Disposed`。
`RequestStop()` 立即关闭准入并启动清理，可在回调中调用。
`Completion` 是稳定的完成任务，包含已启动初始化、显式登记操作、退出、取消、错误交付、
自有页面销毁及子容器异步释放。清理失败保留在该任务中；多个独立失败一起报告。
`DisposeAsync()` 请求停止并返回相同完成任务。同步 `Dispose()` 不阻塞 Dispatcher，
也不承诺返回时资源已经释放。正常关闭必须先等待清理，再销毁宿主和根容器。

WPF 窗口可采用下面的 Closing 协议。若有关闭确认，应在开始清理前完成确认：
排空期间窗口仍存在，应封闭输入；会话切换时也应停用导航菜单，成功挂接新会话后再恢复。
托盘等独立输入入口需检查关闭状态，避免再次调用控件便利方法建立隐式会话。

```csharp
private Task? _closing;
private bool _closeAllowed;

protected override void OnClosing(CancelEventArgs e)
{
    base.OnClosing(e);
    if (_closeAllowed || e.Cancel)
    {
        return;
    }

    e.Cancel = true;
    _closing ??= CloseAfterCleanupAsync();
}

private async Task CloseAfterCleanupAsync()
{
    // 先返回 Closing，使重入关闭能看到已发布的任务。
    await Dispatcher.Yield(DispatcherPriority.Normal);
    try
    {
        IsEnabled = false;
    }
    catch (Exception failure)
    {
        logger.LogError(failure, "关闭输入通知失败。");
    }

    try
    {
        await _navigationScope.DisposeAsync();
        // 若窗口拥有应用关闭：此处再 await 根容器或 Generic Host 的异步释放。
    }
    catch (Exception failure)
    {
        // 按应用策略记录清理失败。若有多个独立拥有者，应逐一尝试并汇总错误。
        logger.LogError(failure, "关闭清理失败。");
    }

    _closeAllowed = true;
    Close();
}
```

生命周期回调、已登记操作和错误事件不能等待包含自己的作用域完成任务；检测到这类
调用会明确抛出 InvalidOperationException。回调只调用 RequestStop，外部关闭协调者负责等待。
正常路径不使用超时后强制销毁依赖。若 Dispatcher 已关闭或在排空途中被强制关闭，
Completion 明确失败，表示受限清理；仍在使用的依赖被保留，不宣称所有 UI 回调已完成。
直接 Application.Shutdown、操作系统强制终止等不能替代正常关闭协议。

## 不使用依赖注入与页面所有权

核心包直接提供 `NavigationScope.Attach(RootNavigation, pageProvider)`。
省略 provider 时使用公开无参构造函数，所创建页面由作用域拥有。
无 DI 的 Services 只提供当前 INavigationService 和 INavigationViewPageProvider。
作用域不销毁调用方传入的 provider；显式作用域也不拥有应用根容器。

自定义 provider 返回 `NavigationPageLease`，创建时即声明最终释放责任：

```csharp
public sealed class ApplicationPageProvider : INavigationViewPageProvider
{
    public NavigationPageLease GetPage(Type pageType)
    {
        if (pageType != typeof(DashboardPage))
        {
            throw new InvalidOperationException($"Unhandled page type: {pageType}");
        }

        var model = new DashboardViewModel();
        try
        {
            var page = new DashboardPage(model);
            return NavigationPageLease.Owned(page, model);
        }
        catch (Exception creationFailure)
        {
            try
            {
                model.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(creationFailure, cleanupFailure);
            }

            throw;
        }
    }
}
```

- `Borrowed(page)`：调用方或容器拥有最终销毁责任，导航只管理激活。
- `Owned(page, model)`：工厂转交页面和可选模型，按引用去重，异步释放优先。
- `Owned(page, releaseAsync)`：例如转交页面子容器，回调负责其完整释放。

导航不会通过 DataContext 猜测所有权。拥有租约应对应唯一、尚未销毁的页面实例；
工厂不能为同一活着的对象反复创建互相冲突的拥有租约。同一页面实例在旧会话排空前，
不能交给另一个会话；借用实例在旧会话完全结束后可以再次交付。已最终释放的自有实例
不能重新交付，即使改成借用租约也会被拒绝。取消候选、缓存淘汰、清空和作用域
停止都释放相应保留权，最后一个激活和缓存保留者离开后才最终销毁。
缓存页面旧任务未结束时，可以显示该实例，但新的进入要等旧激活排空。

直接调用 NavigationView 的 bool 便捷方法会创建隐式无容器会话。
这类调用方在最终关闭前应 `await navigationView.StopNavigationAsync()`。
显式作用域更适合需要注入服务、观察错误、跨宿主路由及协调应用关闭的场景。

## WinForms 与 ElementHost

宿主不要求 WPF Window 或 Application.Current。把 NavigationView 放入 ElementHost 后
挂接作用域。Form 应在 FormClosing 中取消首次关闭、异步等待作用域，完成后再次 Close，
再由 Form 释放 ElementHost。不能在 Dispose(bool) 中阻塞等待 WPF Dispatcher。

```csharp
private bool _closeAllowed;
private Task? _closing;

protected override void OnFormClosing(FormClosingEventArgs e)
{
    base.OnFormClosing(e);
    if (_closeAllowed || e.Cancel)
    {
        return;
    }

    e.Cancel = true;
    _closing ??= CloseAfterCleanupAsync();
}

private async Task CloseAfterCleanupAsync()
{
    await Task.Yield();
    try
    {
        Enabled = false;
    }
    catch (Exception failure)
    {
        logger.LogError(failure, "关闭输入通知失败。");
    }

    try
    {
        await _navigationScope.DisposeAsync();
    }
    catch (Exception failure)
    {
        logger.LogError(failure, "导航清理失败。");
    }

    _closeAllowed = true;
    Close();
}
```

## 页面上下文、进入与后续操作

上下文以一个只读继承附加属性 `Navigation.Context` 原子发布到页面根节点。
它包含 Service、Host、Parameter；三个值属于同一次提交。便利读取方法保留：

```csharp
INavigationService? navigation = Navigation.GetService(this);
INavigationHost? host = Navigation.GetHost(this);
object? parameter = Navigation.GetParameter(this);
NavigationContext? context = Navigation.GetContext(this);
```

绑定使用 `(ui:Navigation.Context).Parameter` 等路径；覆写 OnPropertyChanged 时比较
Navigation.ContextProperty，再从新 NavigationContext 读取参数。
原先独立的 ServiceProperty、HostProperty、ParameterProperty 已移除。
Frame 的属性继承边界使上下文必须直接写入页面，参数不替换 DataContext。

进入前冻结视图、DataContext 和 INavigableView 模型身份，按引用去重。
视图先进入、模型后进入，退出按相反顺序。只有真正调用了进入的参与者才取得退出责任，
部分进入后抛错也需退出；单独实现 INavigationExited 不会收到没有对应进入的退出。
退出使用快照，不重新读取已经变化的 DataContext。

每次 Frame 提交后在 DispatcherPriority.Loaded 交付进入；真实视觉卸载结束激活，
重新加载创建新激活。同一 Dispatcher 周期的视觉移动可以合并。
IAsyncNavigationAware 返回的任务属于初始化责任；需要在初始化后继续执行命令时使用
INavigationActivationAware：

```csharp
public sealed class ArticleViewModel : INavigationActivationAware
{
    private NavigationActivationContext? _activation;

    public Task OnNavigationEnteredAsync(NavigationActivationContext context)
    {
        _activation = context;
        if (context.Target?.Parameter is not int articleId)
        {
            throw new InvalidOperationException("Article navigation requires an integer identifier.");
        }

        return LoadArticleAsync(articleId, context.CancellationToken);
    }

    public Task SaveAsync()
    {
        var activation = _activation
            ?? throw new InvalidOperationException("The page is not active.");
        return activation.RunAsync(token => SaveArticleAsync(token));
    }

    public void OnNavigationExited()
    {
        _activation = null;
        // 解除本次激活的外部订阅。核心会等待初始化和已登记的 SaveAsync 完成。
    }
}
```

RunAsync 必须在 UI 线程调用，它先登记工作再执行委托。把操作及全部依赖收尾放在委托中，
取消后仍需使用依赖的工作也必须等到委托结束。任意未登记的后台任务不在导航完成保证范围内。
同时实现旧异步接口和上下文接口时优先使用上下文接口；同步接口仍先于异步接口调用。

回调内只请求 RequestStop，由作用域拥有者在回调外等待 Completion。即使提前保存了
Completion 的 Task，也不能在该作用域的回调中等待它：该任务本来就包含当前回调。
公开入口可检测回调内的自等待，无法拦截调用方直接 await 已保存 Task 的行为。
检测也覆盖页面租约与 DI 资源的异步释放回调，以及宿主已经解除挂接后的
`StopNavigationAsync` 入口。清理回调应完成自己拥有的资源收尾，由外部拥有者等待作用域。

NavigationCallbackFailed 在所属 Dispatcher、依赖释放前交付。
NavigationCallbackException 记录 Entered、Exited 或 Operation 阶段、页面、参与者和目标。
处理器设置 Handled=true 表示应用已经处理；未处理错误进入所属作用域的 Completion。
RunAsync 返回的任务仍保留操作异常，调用方捕获该任务异常不会自动设置错误事件的 Handled。
预期失败需要在明确的应用错误处理边界标记已处理，才能使作用域最终以成功完成。
处理器自身抛错与原异常一起保存，不递归通知。同一异常从嵌套已登记操作传播时仅通知一次。
取消源在全部工作收尾后释放；取消是停止请求，不代表工作已经结束。

## 挂接失败与迁移检查

挂接发生同步失败且回滚已完成时，直接抛出原始或聚合异常。需要异步回滚时抛出
NavigationAttachmentException，调用方必须等待其中的 Completion 后再销毁根依赖。
失败作用域的路由按服务身份撤销，不会误删新会话或同名既有会话。

迁移调用方时检查：provider 返回显式租约；请求等待 Completion；正常关闭迁移至
Closing/FormClosing；后续命令纳入激活上下文；附加属性改为 Context 快照；
生命周期回调使用 RequestStop，作用域拥有者观察完整清理结果。
