# INavigationAware 设计笔记

> 状态：**讨论结论，未实施**
> 日期：2026-08-24
> 范围：`INavigationAware`、`NavigationView.NavigationFrame.cs`、`INavigationScope` 的处置链

这份笔记记录的是「目标语义」，不是改造方案。等 `NavigationView` 其余部分梳理清楚后，
它充当改造的验收标准；如果中途发现某条语义站不住，改这里比改代码便宜。

---

## 1. 起因

导航在 UI 层看是同步的，但通知到 `INavigationAware` 时已经变成异步，且：

- `OnNavigationEnteredAsync` 是异步的，却没有 `CancellationToken`；
- `OnNavigationExitedAsync` 照对称写成异步，但它的 `Task` 只服务于一个边界；
- 为支撑上述两点，`NavigationFrame` 里堆了一套串行通知队列。

---

## 2. 事实核查

### 2.1 同步性不是在 `INavigationAware` 这一层丢掉的

`Frame.Navigate(object)` 本身就是异步的。WPF 的 `NavigationService.Navigate(Object root, Object navigationState)`
最后一步是 `_navigateQueueItem.PostNavigation()`（`NavigationService.cs:1726`），而它是
`BeginInvoke(DispatcherPriority.Normal, ...)`（`NavigationService.cs:4428-4432`）。

真正同步的是我们自己的路由事件：`NavigateInternal` 在 `UpdateContent()` 发起 frame 导航之后
立刻 `OnNavigated(pageInstance)`（`NavigationView.Navigation.cs:384` 与 `:429`）。

完整时间线：

| 时刻 | 优先级 | 发生什么 |
| --- | --- | --- |
| T0 | `Navigate()` 调用栈内 | journal、`SelectedItem`、`CurrentLocation` 已更新，**路由事件 `Navigated` 触发**；frame 仅排队 |
| T1 | Normal（WPF 自己 post） | frame 换内容 → `Frame.Navigated` → `OnNavigationFrameNavigated` |
| T2 | Loaded（我们 post） | `OnNavigationExitedAsync` → `OnNavigationEnteredAsync` |

async 化只增加了 T1→T2 这一跳，T0→T1 的延迟是 WPF 自带的。

### 2.2 T2 的 Loaded 优先级与 async 无关，是硬需求

分派逻辑靠 `frameworkElement.DataContext` 决定通知谁（`NavigationView.NavigationFrame.cs:391-434`）。
`Frame.Navigated` 触发时，frame 模板里的 `ContentPresenter` 尚未挂上页面。
`DispatcherPriority.Loaded` 位于 `Input` 与 `Render` 之间，其文档语义正是
"processed when layout and render is done ... used while firing the Loaded event"
（WPF `DispatcherPriority.cs:55-68`），恰好落在布局与 `Loaded` 事件之后。

**结论：即使接口改回 `void`，这一跳也必须保留。**「改同步就能立刻收到通知」做不到。

### 2.3 `Loaded`/`Unloaded` 不能替代本接口

三个方向都验证过：

**(a) 漏报——缓存页面换参数重导航，`Loaded` 一次都不触发**

```csharp
// ArticlePage 设了 NavigationCacheMode.Permanent
navigation.Navigate(typeof(ArticlePage), articleId: 1);   // Loaded ✔
navigation.Navigate(typeof(ArticlePage), articleId: 2);   // Loaded ✘
```

链路：

1. `IsRedundantRequest` 因参数不同返回 `false`（`NavigationView.Navigation.cs:443`），导航照常进行；
2. `GetNavigationItemInstance` → `_cache.Remember(targetPageType, ...)`，**缓存按 `Type` 索引**，
   返回同一个实例（`NavigationView.Navigation.cs:506`、`NavigationCache.cs:11-12`）；
3. WPF 侧 `ReferenceEquals(root, _bp)` 为真 → 走 `NavigateToFragmentOrCustomContentState`
   （`NavigationService.cs:1704-1706`），**不换内容、不碰可视树**；
4. 但该方法末尾仍然 `HandleNavigated(...)` → `FireNavigated(...)`
   （`NavigationService.cs:326-328`、`:2361`）。

所以 `Frame.Navigated` 照常触发、`INavigationAware` 照常收到通知，而页面的 `Loaded` 永远不来。
只监听 `Loaded` 的页面会一直显示文章 1，而 journal 与 `CurrentLocation` 说的是文章 2。

「列表 → 详情(id) → 返回 → 另一个详情(id)」是最常见的导航模式，`NavigationCacheMode` 正是为它准备的。

**(b) 误报——没导航却触发 `Unloaded`**

改 `PaneDisplayMode` 会把同一个 frame 在模板宿主之间搬家，瞬时触发 `Unloaded`；
控件为此专门延迟到 `ContextIdle` 甄别（见 `NavigationView.NavigationFrame.cs:254-261` 的注释）。
把这个责任推给页面，等于每个页面重抄一遍甄别逻辑。NavigationView 放进 `TabControl` 里切页签同理。

**(c) ViewModel 根本没有 `Loaded`/`Unloaded`**

接口真正在做的事是找到 content 的 `DataContext` 或 `INavigableView.ViewModel`，通知**视图模型**。
纯 VM 在 DI 容器里没有可视树事件。取消接口意味着每个页面都要写
`Loaded += (_, _) => (DataContext as MyViewModel)?.OnEnter();`，再各自处理 (a)(b) 两条。
这不是变简单，是把复杂度从一处搬到 N 处。

**结论：接口保留。**

---

## 3. 当前实现的三个问题

### 3.1 `async void` 边界把页面异常变成不可定位的崩溃

`OnNavigationFrameNavigated` / `OnNavigationFrameUnloaded` 都是 `async void`，
而 `NotifyNavigationTransitionAsync` 还刻意 `exitException?.Throw()` 抛回去。
`async void` 的「observe」就是在一个与导航调用点无关的 dispatcher 轮次上抛未处理异常——
应用崩溃，且栈里看不出是谁触发的导航。同步 `void` 至少会在 `Navigate()` 的栈里炸。

附带缺陷：`NavigationView.NavigationFrame.cs:194` 的 Enter 若抛异常，`exitException?.Throw()`
永远不会执行，Exit 的异常被静默丢弃。

### 3.2 队头阻塞，且无取消

通知队列是 per-NavigationView 串行的。某页面在 `OnNavigationEnteredAsync` 里 `await` 了 5 秒网络请求，
后续所有导航的 Enter/Exit 都被压后 5 秒——而新页面早已显示在屏幕上，它的 VM 还没收到 Enter。
快速连点侧边栏即可复现。

对比 Blazor 的 `OnInitializedAsync`：同为 `Task`，但它是 per-component 的，上一个慢不会卡住下一个。

### 3.3 async Exit 撑起了一整条不必要的处置链

- `INavigationHost.DetachNavigationScopeAsync` 必须返回 `Task`（`NavigationView.Navigation.cs:200`）
- `INavigationScope` 必须同时有 `Dispose()` 与 `DisposeAsync()`（`INavigationScope.cs:35`）
- 于是有了 `NavigationScope.cs:86-95` 的妥协：同步 `Dispose()` 遇到异步页面会**静默地不完成清理**

---

## 4. 目标语义

### 4.1 接口形状

```csharp
public interface INavigationAware
{
    /// <summary>页面进入当前导航状态。</summary>
    /// <param name="cancellationToken">本次导航被后续导航取代、或宿主分离时取消。</param>
    Task OnNavigationEnteredAsync(CancellationToken cancellationToken);

    /// <summary>页面离开当前导航状态。</summary>
    void OnNavigationExited();
}
```

**Enter 异步**：页面要做事（加载数据），做事就会被打断，所以必须带令牌。

**Exit 同步**：Exit 的异步性从来不是页面的需求——导航在 `Frame.Navigated` 时早已提交，
`await` 它既不能取消也不能回退。它唯一服务的边界是 `NavigationScope.DisposeAsync()`
等待 scoped 依赖释放。放弃这个边界，换取第 5 节的整条简化。

**取舍记录**：代价是页面无法做「被宿主等待的」异步退出清理。若确有需求，
应由应用自身的保存/关闭机制承担，而不是一个在导航已提交后才触发、且无法阻塞的 UI 回调。

### 4.2 触发时刻

- 均在 T2（`Frame.Navigated` 之后 post 到 `DispatcherPriority.Loaded` 的那一跳）触发；
- 顺序固定为先 `OnNavigationExited()`（旧内容）后 `OnNavigationEnteredAsync()`（新内容）；
  Exit 同步使该顺序自然成立，不再需要队列；
- 与路由事件 `Navigated`（T0）之间隔两个 dispatcher 轮次和一次完整布局，
  **此差异必须在 API 文档中显式写明**——同时使用两者的消费者会看到不一致状态。

### 4.3 CancellationToken 取消时机

令牌绑定到**导航次序**，而非页面生命周期。

NavigationView 持有单个 CTS：

- 在下一次 `Frame.Navigated` 提交时 `Cancel()` + `Dispose()`，随即为新导航建新的；
- 在 `Detach` 时 `Cancel()` + `Dispose()`。

语义：「你正在为之加载的那次导航已经不再是当前导航」。页面 A 加载到一半用户切走，
A 的令牌立刻取消，continuation 不会再往已不可见的 VM 上写数据。

类比 CommunityToolkit 的 `AsyncRelayCommand`：成立的部分是**宿主持有 CTS、使用者只拿 token**，
省掉每个页面自己写 `_cts?.Cancel(); _cts = new()`。

### 4.4 异常出口

Exit 改同步后，Enter 返回的 `Task` **没有任何人 await**，因此必须有显式故障出口，
不能继续依赖 `async void`。

- NavigationView 上暴露失败事件（暂定 `NavigationCallbackFailed`）；
- 默认行为仍然重抛，保持 fail-fast；
- 异常需包装并带上页面类型与导航目标，保证可定位；
- `OperationCanceledException` 且令牌已取消时，视为正常路径，不上报。

### 4.5 缓存重导航

同一实例因参数变化被重新导航（第 2.3(a) 节场景）**算一次完整的进出**：
先对该实例 `OnNavigationExited()`，再对同一实例 `OnNavigationEnteredAsync()`。
语义为「你离开了文章 1，进入了文章 2」。当前实现已是此行为，改造时须保留并补测试。

### 4.6 分派规则（维持现状）

按 `NavigationView.NavigationFrame.cs:391-434` 的既有次序：

1. content 实现 `INavigationAware` → 通知 content；若其 `DataContext` 是另一个
   `INavigationAware`，随后也通知（视图异常不阻断视图模型回调）；
2. content 是 `INavigableView<object>` 且 `ViewModel` 实现接口 → 通知 ViewModel；
3. content 是 `FrameworkElement` 且 `DataContext` 实现接口 → 通知 DataContext。

---

## 5. 改造时可以删掉什么

按目标语义实施后，以下内容应一并移除：

- `NavigationView.NavigationFrame.cs:279-345` 整套通知队列
  （`_navigationNotificationTask`、`TaskCompletionSource`、`ContinueWith` + `Unwrap`、`CheckAccess` 编组），约 70 行；
- `INavigationHost.DetachNavigationScopeAsync` 的 `Task` 返回值 → `void`；
- `INavigationScope.DisposeAsync()`（`INavigationScope.cs:35`），只保留 `IDisposable`；
- `NavigationScope.cs:86-95` 的 `IsCompleted` 妥协及其注释；
- `Wpf.Zen.Ui.DependencyInjection/NavigationScope.cs` 中的对应实现；
- 测试 `NavigationScopeTests.DisposeAsync_WaitsForPageExitBeforeDisposingScopedDependencies`
  及其 `AsyncExitPage` 夹具。

Gallery 侧 `ViewModels/ViewModel.cs` 的 `OnNavigationEntered()` / `OnNavigationExited()`
同步包装可以简化：Exit 直接就是同步方法，Enter 保留 `Task` + 令牌。

---

## 6. 未决问题

1. **是否需要 `IsContentLoading` 之类的状态量**（对应 `AsyncRelayCommand` 的 `IsRunning`），
   供进度环绑定？属于独立议题，不阻塞本次语义定稿。
2. **破坏性变更的投放方式**：三处公开 API 变化（接口签名、`INavigationScope`、`INavigationHost`）。
   一次改净，还是留过渡期？
3. **路由事件 `Navigated` 在 T0 触发是否合理**。它早于 frame 换内容，与 `INavigationAware` 差两个轮次。
   考虑是否也移到 T2，或至少在文档中标注两者的定位差异。此项与 async 无关，可单独处理。

---

## 7. 明确不做的事

- 不取消 `INavigationAware`（第 2.3 节三条实证）；
- 不取消 T2 的 Loaded 优先级延迟（第 2.2 节）；
- 不在本轮内重构 `NavigationView` 其余部分——先梳理清楚再动刀。
