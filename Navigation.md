# 导航服务使用说明

WPF Zen UI 的导航以“一个导航宿主对应一个导航作用域”为基本原则。每个
`NavigationView` 都拥有独立的导航服务、页面实例、缓存、历史和生命周期，多个窗口、
多个 Tab 或多个 `ElementHost` 之间不会因注册顺序而互相覆盖。

`NavigationView` 实现了 `INavigationHost`。应用应为每个长期存在的导航宿主创建一个
`INavigationScope`，并在宿主永久关闭时释放它。不要把 `Loaded`/`Unloaded` 当作作用域
生命周期，因为 WPF 在换模板或重新挂接视觉树时也可能暂时触发这些事件。

## 使用依赖注入

先注册导航基础设施以及页面、视图模型：

```csharp
services.AddNavigation();

services.AddSingleton<MainWindow>();
services.AddScoped<MainWindowViewModel>();

// 页面和页面视图模型通常注册为 transient。
// 页面是否复用由各 NavigationView 自己的 NavigationCacheMode 决定。
services.AddTransient<DashboardPage>();
services.AddTransient<DashboardViewModel>();
services.AddTransient<SettingsPage>();
services.AddTransient<SettingsViewModel>();
```

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

默认的 `ActivatorNavigationViewPageProvider` 使用页面的无参构造函数。需要构造函数参数、
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
