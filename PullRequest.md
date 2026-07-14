优化：解耦导航按钮的标题与图标呈现策略

## Pull request type

- [ ] Update
- [ ] Bugfix
- [x] Feature
- [ ] Code style update (formatting, renaming)
- [ ] Refactoring (no functional changes, no api changes)
- [ ] Build related changes
- [ ] Documentation content changes

## 问题

`NavigationView` 在代码中创建带固定字重的 `TextBlock`，并由 `UpdatePaneButtonPlacement` 直接覆盖窗格切换按钮的 `Content`。这使标题的具体呈现方式被控件实现锁定，同时阻止应用模板和样式独立使用按钮内容。

## 解决方式

- `NavigationViewButton` 通过只读 `PaneTitle` 依赖属性实时公开所属导航视图的标题状态，不再要求标题占用 `Content`。
- 新增只读 `Role` 状态，使模板可以明确区分返回按钮与窗格切换按钮，而无需依赖图标、名称或自动化标识。
- `NavigationView` 不再创建 `_paneTitle`，也不再设置按钮 `Content` 或在代码中指定标题字重。
- 默认模板仅在窗格切换按钮没有外部 `Content` 时呈现 `PaneTitle`，并使用按钮应有的访问键识别机制；外部内容始终优先。
- `NavigationView` 不再构造或指定命令图标；默认样式根据 `Role` 选择返回和窗格切换 glyph，模板只负责呈现图标插槽。
- `NavigationViewButton.Icon` 改为不受 `IconElement` 限制的 `object` 内容插槽，并移除不再适用的 `IIconControl` 契约。
- 外部 `Icon` 值优先于默认 glyph，可直接使用视觉元素、普通对象或隐式数据模板，不需要额外实现 `IconSource`。
- 修复 `IconSourceElement` 无法作为视觉元素直接呈现的问题；通过稳定的内部 presenter 支持 `IconSource` 在空值、非空值、替换和清空之间安全切换。

## Other information

标题、图标状态和视觉呈现现在具有清晰边界。应用可在自定义模板中绑定 `PaneTitle` 和 `Role`，自行选择标题控件及图标策略；切换窗格显示模式、开关窗格及更新标题均不会改写应用提供的按钮内容或图标。`IconSourceElement` 现在也可安全放入不带 `IconElement.Coerce` 的普通对象内容插槽。
