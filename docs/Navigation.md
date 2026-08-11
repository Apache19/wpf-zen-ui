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

## 使用依赖注入

先注册导航基础设施以及页面所依赖的视图模型或服务。页面类型本身无需注册：

```csharp
services.AddNavigation();

services.AddSingleton<MainWindow>();
services.AddScoped<MainWindowViewModel>();

// 只注册页面的构造函数依赖。
services.AddTransient<DashboardViewModel>();
services.AddTransient<SettingsViewModel>();
```

`AddNavigation()` 不注册也不要求应用注册 `INavigationViewPageProvider`。导航时会先尊重页面
类型已有的显式 DI 注册；页面未注册时，则由当前导航宿主的子作用域直接创建，并为其注入
已注册的构造函数依赖。因此，只有页面需要自定义工厂、替换实例或特殊生命周期时，才需要
显式注册页面类型。

窗口在初始化控件后创建自己的导航作用域：

```csharp
public partial class MainWindow
{
    private readonly INavigationScope _navigationScope;

    public MainWindow(INavigationScopeFactory navigationScopes)
    {
        InitializeComponent();

        _navigationScope = navigationScopes.Attach(RootNavigation, "main");
        DataContext = _navigationScope.Services
            .GetRequiredService<MainWindowViewModel>();
    }

    public INavigationService Navigation => _navigationScope.Navigation;

    protected override void OnClosed(EventArgs e)
    {
        _navigationScope.Dispose();
        base.OnClosed(e);
    }
}
```

`Attach` 会创建一个独立的 DI 子作用域。页面或视图模型注入的
`INavigationService` 只会控制这个作用域对应的 `RootNavigation`：

```csharp
public sealed class DashboardViewModel(INavigationService navigation)
{
    public void OpenSettings()
    {
        _ = navigation.Navigate(typeof(SettingsPage), new SettingsRequest());
    }
}
```

如果页面的 `OnNavigationExitedAsync()` 会执行真正的异步清理，并且调用方需要等待清理
和 scoped 服务释放完毕，应使用：

```csharp
await _navigationScope.DisposeAsync();
```

同步 `Dispose()` 不会阻塞 UI 线程等待未完成的异步退出回调，以避免死锁。

### 显式跨宿主导航

只有确实需要从一个作用域控制另一个作用域时才使用 `INavigationRouter`。路由不会猜测
“当前窗口”，必须传入目标作用域在 `Attach` 时声明的 Id：

```csharp
await router.NavigateAsync(
    "secondary-window",
    NavigationRequest.To<DetailsPage>(customerId)
);
```

目标窗口关闭并释放作用域后，路由会返回 `NavigationResultStatus.HostNotFound`。同步路由
要求调用线程就是目标宿主的 Dispatcher；不确定线程归属时应使用异步方法。

## 不使用依赖注入

核心包直接提供 `NavigationScope.Attach`。它不要求引用
`Wpf.Zen.Ui.DependencyInjection`，但仍然具有与 DI 模式相同的导航上下文、请求参数、
缓存、历史和释放行为：

```csharp
public partial class MainWindow
{
    private readonly INavigationScope _navigationScope;

    public MainWindow()
    {
        InitializeComponent();

        _navigationScope = NavigationScope.Attach(RootNavigation);
        _ = _navigationScope.Navigation.Navigate(
            typeof(DashboardPage),
            parameter: "startup"
        );
    }

    protected override void OnClosed(EventArgs e)
    {
        _navigationScope.Dispose();
        base.OnClosed(e);
    }
}
```

默认的 `ActivatorNavigationViewPageProvider` 使用页面的无参构造函数。需要自定义构造规则、
已有页面实例或自定义创建规则时，实现 `INavigationViewPageProvider`：

```csharp
public sealed class ApplicationPageProvider : INavigationViewPageProvider
{
    public object? GetPage(Type pageType)
    {
        if (pageType == typeof(DashboardPage))
        {
            return new DashboardPage(new DashboardViewModel());
        }

        return ActivatorNavigationViewPageProvider.Instance.GetPage(pageType);
    }
}
```

然后在创建作用域时传入：

```csharp
_navigationScope = NavigationScope.Attach(
    RootNavigation,
    new ApplicationPageProvider()
);
```

作用域不会取得调用方传入的页面提供器的所有权，也不会在释放时自动 Dispose 它。页面
是否缓存仍由 `NavigationViewItem.NavigationCacheMode` 决定，而不是由页面提供器决定。

无 DI 作用域的 `Services` 是一个最小 `IServiceProvider`，可取得当前作用域的
`INavigationService` 和 `INavigationViewPageProvider`；其他服务类型返回 `null`。

## WinForms 与 ElementHost

导航宿主不要求 WPF `Window` 或 `Application.Current`。在 WinForms 中，可以直接把
`NavigationView` 放进 `ElementHost`，并让 Form 或承载控件拥有作用域：

```csharp
private readonly NavigationView _navigationView = new();
private readonly INavigationScope _navigationScope;

public MainForm()
{
    InitializeComponent();

    elementHost.Child = _navigationView;
    _navigationScope = NavigationScope.Attach(_navigationView);

    _ = _navigationScope.Navigation.Navigate(typeof(DashboardPage));
}

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _navigationScope.Dispose();
    }

    base.Dispose(disposing);
}
```

如果 WinForms 应用本身使用 Microsoft.Extensions.DependencyInjection，也可以改用
`INavigationScopeFactory.Attach`；宿主仍然是 `NavigationView`，不需要制造一个虚假的
WPF Window。

## 页面中的导航上下文

导航请求的参数不会覆盖页面的 `DataContext`。导航后的页面及其逻辑树后代可以读取：

```csharp
INavigationService? navigation = Navigation.GetService(this);
INavigationHost? host = Navigation.GetHost(this);
object? parameter = Navigation.GetParameter(this);
```

这些值由作用域以只读继承附加属性发布到页面根节点。之所以直接发布到页面，是因为 WPF
`Frame` 会形成属性继承边界，仅把值设置到外层 `NavigationView` 无法可靠到达页面内部。

非可视对象和视图模型在 DI 模式下应优先通过构造函数注入 `INavigationService`，而不是
依赖视觉树查找。

## 使用约束

- 一个 `INavigationHost` 同一时间只能附加一个作用域；重复附加会抛出异常。
- 创建、同步导航和附加作用域必须在宿主 Dispatcher 上执行；跨线程调用使用异步导航。
- 导航作用域应由真正拥有宿主生命周期的窗口、Tab、Form 或承载控件保存并释放。
- 不要把 `INavigationService` 注册成应用级 singleton；`AddNavigation()` 会主动替换旧的
  `INavigationService` 注册，以保证每个宿主使用独立 scoped 实例。
- 可以直接调用 `NavigationView.Navigate(...)` 进行低层控件导航，但需要完整 Service、
  Host、Parameter 和释放语义时，应始终先创建导航作用域。
