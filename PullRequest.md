修复：修正 NavigationView 首次跨 PaneDisplayMode 模板切换异常

## Pull request type

- [ ] Update
- [x] Bugfix
- [ ] Feature
- [ ] Code style update (formatting, renaming)
- [ ] Refactoring (no functional changes, no api changes)
- [ ] Build related changes
- [ ] Documentation content changes

## What is the current behavior?

Issue Number: N/A

`Left` 与 `LeftFluent` 原本复用 `LeftNavigationViewTemplate`，`LeftFluent` 仅通过把 `OpenPaneLength` 设置为 `NaN` 模拟自适应宽度。首次从 `Left` 切换时 WPF 判断模板实例没有变化，因此不会清理并重建模板视觉树，导致 AutoSuggestBox、PaneHeader、顶部分割线等仅属于 `Left` 的元素继续显示，Pane 宽度也会由旧模板按错误的可用空间参与布局。

改用独立模板后还会暴露两个关联问题：模板生成的 `NavigationViewContentPresenter` 会随旧视觉树一起销毁，当前页面因此丢失；旧 Pane 宽度动画在模板绑定被清理时仍可能使用 `Width=NaN` 求值并抛出异常。

## What is the new behavior?

- 为 `LeftFluent` 提供独立的 `FluentNavigationViewTemplate`。该模板的 Pane 使用 `Auto` 列，仅包含菜单、Footer 及 Footer 分割线，不再生成命令按钮、PaneHeader、AutoSuggestBox 或顶部分割线。
- 将 `NavigationViewContentPresenter` 提升为 `NavigationView` 生命周期内的稳定实例，由各模板通过 `PART_NavigationViewContentPresenterHost` 承载。切换模板时保留当前页面、Frame 日志和导航状态。
- 在跨模板模式切换前停止并移除旧 Pane 的宽度动画，避免 WPF 清理模板绑定时以 `NaN` 作为动画基值。
- 模板临时迁移 Frame 时不再误触 `OnNavigatedFromAsync`；控件真正卸载时仍执行页面离开通知。
- `LeftFluent` 不再覆盖 `IsBackButtonVisible` 与 `IsPaneToggleVisible`，返回其他显示模式后调用方配置保持不变。
- 重新应用模板时解除旧 AutoSuggestBox 按钮事件并同步新模板的 Pane 视觉状态。

## Other information

- 已参考本地 WPF 8.0.28 源码中 `StyleHelper.UpdateTemplateCache`、`StyleHelper.DoTemplateInvalidations`、`StyleHelper.ClearGeneratedSubTree` 与 `FrameworkElement.ApplyTemplate` 的实现确认模板失效时序。
- `dotnet build Wpf.Ui.slnx --no-restore`：通过（0 errors，存在仓库原有 warnings）。
- `dotnet test tests/Wpf.Ui.UnitTests/Wpf.Ui.UnitTests.csproj --no-restore --filter "FullyQualifiedName~NavigationViewTests"`：通过（2/2）。
- 完整单元测试：122 通过、1 失败。失败项 `NavigationViewItemTests.ItemStyle_SelectsTopLevelAndSubItemTemplatesFromPaneDisplayMode` 在本次修改前已存在，测试仍引用重构前的资源键 `LeftCompactNavigationViewItemTemplate`，与本修复无关。
