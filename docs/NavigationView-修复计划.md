# NavigationView 导航机制修复计划

## 问题诊断

### 三个词典的本意

| 词典 | 键 → 值 | 实际职责 |
|------|---------|----------|
| `PageIdOrTargetTagNavigationViewsDictionary` | string → INavigationViewItem | 通过字符串找到菜单上的按钮 |
| `PageTypeNavigationViewsDictionary` | Type → INavigationViewItem | 通过页面类型找到菜单上的按钮 |
| `PageToNavigationItemDictionary` | FrameworkElement → INavigationViewItem | 渲染后的页面反向找到自己的按钮（用于面包屑） |

这三个词典存储的都是 UI 对象——**菜单按钮**（`NavigationViewItem` 继承自 `ButtonBase`）。它们在 UI 上有视觉效果（高亮、激活态），生命周期属于视觉树。

### 当前设计的问题

1. **导航引擎被 UI 对象劫持**

   ```
   Navigate(string) ─→ 词典查 Button ─→ 取 Button.TargetPageType ─→ 创建页面
   Navigate(Type)   ─→ 词典查 Button ─→ 取 Button.TargetPageType ─→ 创建页面
   GoBack()         ─→ Journal(Id)  ─→ 词典查 Button ─→ 取 Button.TargetPageType ─→ 创建页面
   ```

   每一步导航都被迫先找到菜单按钮，再从按钮上取 `TargetPageType`，然后才能创建页面。如果一个页面在菜单上没有对应按钮，代码就当场 `new NavigationViewItem(pageType)` 创建一个永不可见的"幻影"按钮塞进词典（见 `TryToNavigateWithoutINavigationViewItem`）。

2. **`Navigate(string)` 和 `Navigate(Type)` 行为不对等**

   - `Navigate(Type)` 在词典找不到时，会创建幻影 `NavigationViewItem` fallback 导航
   - `Navigate(string)` 在词典找不到时，**直接返回 false**——因为只有 string 不知道页面类型

3. **历史记录绑定 UI 对象**

   `Journal` 存储的是 `NavigationViewItem.Id`（随机 GUID）。回退时必须通过词典把 Id 翻译回 NavigationViewItem，再取 TargetPageType 导航。一旦词典里没有这个 Id（菜单项被移除、词典被重建、页面没有菜单项），GoBack 直接崩溃。

4. **词典职责混乱**

   词典既要"反映 UI 上实际有哪些菜单按钮"（这是正确的），又要"支撑导航引擎和历史记录运行"（这是错误的）。维护词典同步（Add/Remove/Rebuild）时，不得不担心"删除某个条目会不会让 GoBack 崩溃"，于是陷入两难。

### 正确的边界

```
┌─────────────────────────────────────────────────┐
│  导航引擎                                        │
│  - 知道如何从 Key 解析出页面 Type                 │
│  - 知道如何创建页面实例                          │
│  - 管理导航历史（不依赖 UI）                      │
│  - 决定导航能否成功                              │
└──────────────────┬──────────────────────────────┘
                   │ 导航成功后通知
                   ▼
┌─────────────────────────────────────────────────┐
│  UI 层                                          │
│  - 词典纯粹管理"菜单按钮 ↔ 页面"的映射关系       │
│  - 导航发生时，同步菜单项的选中/激活状态          │
│  - 菜单按钮增删时，准确更新词典（不再缩手缩脚）  │
└─────────────────────────────────────────────────┘
```

**导航引擎负责"能不能去"，UI 层负责"去了之后菜单上怎么显示"。两者各管各的。**

---

## 修复目标

1. **导航引擎独立运行**，不依赖任何词典或 UI 对象
2. **`Navigate(string)`、`Navigate(Type)` 行为一致**，都不会因为词典里没有条目而失败
3. **`GoBack()` 不依赖词典**——历史记录存的是页面标识，不是按钮 GUID
4. **词典恢复精确同步**——菜单加了就加，删了就删，Rebuild 就 Rebuild，不再担心破坏导航
5. **消除"幻影 NavigationViewItem"**——不再动态创建不可见的 UI 控件当作数据容器

---

## 实现方向（供参考，不强制）

以下是大致方向，具体实现请根据代码实际审慎判断：

### A. 导航引擎层

- 引入一个纯数据的导航目标标识（不是 NavigationViewItem，不是 Type，考虑 WPF 原生 `Page` 体系的做法——用 URI 或自定义 key）
- `Navigate(key)` 解析出页面实例后直接导航，不查词典
- `Journal` 存储导航目标标识本身（而非 UI 控件的 Id）
- `GoBack()` 从 Journal 取回标识，调用导航引擎，不碰词典

### B. Tag 映射层（可选）

- `string → 页面标识` 的映射独立于菜单按钮存在
- 可以在 XAML 中声明，也可以代码注册
- 一个页面有多个 Tag 别名也没问题

### C. UI 同步层

- 导航成功后，遍历词典找到对应页面的菜单按钮，更新选中/激活状态；没有对应按钮就不更新（不高亮任何菜单项）
- 词典只反映当前 UI 树中实际存在的菜单按钮
- `Add`/`Remove`/`Rebuild` 精确同步，不再有后顾之忧
- `TryToNavigateWithoutINavigationViewItem` 的逻辑移除或重构——导航引擎不应创建 UI 控件

### D. 关键文件

- `src/Wpf.Ui/Controls/NavigationView/NavigationView.Navigation.cs` — 核心导航逻辑
- `src/Wpf.Ui/Controls/NavigationView/NavigationView.Base.cs` — 词典定义和维护方法
- `src/Wpf.Ui/Controls/NavigationView/NavigationView.Properties.cs` — 集合变更回调
- `src/Wpf.Ui/Controls/NavigationView/NavigationViewItem.cs` — 导航项 UI 控件

### E. 相关文件

- `rc\Wpf.Ui\INavigationService.cs` - 导航服务接口，定义导航服务的公开 API
- `rc\Wpf.Ui\NavigationService.cs` - 导航服务实现
- `rc\Wpf.Ui\Extensions\NavigationServiceExtensions.cs`

导航服务依赖导航视图，提供了统一的导航 API，并且是外部调用导航的主要入口。修复 NavigationView 的导航机制时，也要确保导航服务的实现与新的设计兼容，必要时进行调整。

---

## 注意事项

- `NavigationViewItem` 是 UI 控件（继承 ButtonBase），不是数据对象。不要在非 UI 路径上创建它
- `PageToNavigationItemDictionary` 用于面包屑 HeaderContent 更新，这个功能需要保留
- 注意兼容现有 API：`Navigate(string)`、`Navigate(Type)`、`GoBack()` 是公开接口
- 本次修复仅涉及 NavigationView 导航机制，不影响其他控件
