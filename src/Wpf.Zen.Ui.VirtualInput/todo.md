目前的扩展能力不均衡：布局数据、自定义按键和设置菜单已有公开入口；候选来源、提交和翻页则仍固定在内部后端。另一个具体缺口是：外部能生成新布局，却不能直接把新布局标识注册进现有“附加布局”选择机制。

看完当前公开 API 和调用路径，结论是：**常规界面定制已经比较方便，但要让外部独立适配输入法，扩展能力还不够。最明显的缺口就在候选行为。**

如果目标是“用户安装我们的库后，能在自己的项目里完成适配，不必修改库源码”，目前各部分的情况如下：

| 扩展内容 | 当前能力 | 是否方便 |
|---|---|---|
| 键盘布局、行列、键位、页面 | `ISoftKeyboardLayoutProvider`、组合提供器、条件规则、自定义页面标识 | 比较完善 |
| 针对特定输入法替换布局 | 布局上下文包含语言、ClassId、ProfileId、IME 状态等 | 比较完善 |
| 新增附加布局并接入设置选择 | 可生成布局，但可选布局目录和选择校验写在内部 | **存在断点** |
| 自定义按键功能、长按备选行为 | `ISoftKeyboardKeyAction`、`ISoftKeyboardAlternateKeyAction` | 普通同步动作方便 |
| 更复杂的输入操作 | 动作上下文只提供文字、按键及少数操作；没有通用 Ctrl/Alt 组合键、可取消异步输入契约 | 有限 |
| 设置菜单的内容、图标、文字、动作 | `ISoftKeyboardSettingsProvider` 和菜单节点模型，可组合默认设置 | 比较完善 |
| 候选外观 | 可更换样式和模板 | 基础定制可以 |
| 候选排列、滚动、容器生成 | 内部固定横向面板，候选列表为 `sealed`，没有公开面板替换入口 | 不方便 |
| 候选来源、选择、提交、翻页、接管策略 | 后端和关键方法均为内部实现 | **缺少正式扩展入口** |
| 自定义编辑器接入 | `ISoftKeyboardTargetAdapter` 管理可输入性、焦点和输入作用域 | 边界清楚 |
| 输入法状态适配 | `ISoftKeyboardInputStateAdapter` 可替换中英文状态读取和切换 | 仅覆盖状态 |
| 输入法列表、切换菜单、底层输入服务 | 大部分固定在内部 | 不方便 |

最需要补齐的是下面几个地方。

**第一，候选后端虽然已经抽了接口，但目前只是测试入口。**

[SoftKeyboardPlatformServices.cs](E:/Repos/WPF/wpfui/src/Wpf.Zen.Ui.VirtualInput/Infrastructure/SoftKeyboardPlatformServices.cs:10) 的注释明确说明：后端替换用于测试，不是应用扩展点。外部无法注入候选后端，也无法通过继承覆盖私有的提交方法。

这意味着，外部开发者即使掌握某个输入法的专用提交接口，也不能方便地保留现有 TSF 候选读取，仅替换“点击后如何提交”。小狼毫的识别、数字提交和回退判断仍直接写在 [TsfCandidateSession.cs](E:/Repos/WPF/wpfui/src/Wpf.Zen.Ui.VirtualInput/Interop/TextServicesFramework/TsfCandidateSession.cs:202) 中。

候选列表确实有冒泡事件，可以在列表处截获并自行处理，但这种办法需要外部重新负责输入目标校验、重复提交、会话失效等问题，不能算完整的适配契约。

**第二，自定义布局与附加布局设置没有完全接通。**

例如外部实现一个“日语自定义九宫格”，布局提供器能够生成它，设置提供器也能添加菜单项；但调用 `SelectSupplementalLayout("MyJapaneseGrid")` 时，会被内部目录校验拒绝。[相关代码](E:/Repos/WPF/wpfui/src/Wpf.Zen.Ui.VirtualInput/Controls/SoftKeyboard.Settings.cs:144)

用户可以另存自己的选择状态，再调用 `RefreshLayout()` 绕过去，但布局识别、选择记忆等功能就要再实现一遍。这里应该提供统一的布局注册机制，让菜单、默认选择、选择校验和按输入法记忆使用同一份数据。

**第三，更换候选界面仍然容易碰到内部实现。**

当前候选列表内部直接创建横向 `StackPanel`，没有向外提供 `ItemsPanel` 等对应入口。[相关代码](E:/Repos/WPF/wpfui/src/Wpf.Zen.Ui.VirtualInput/Controls/SoftKeyboardCandidateItemsHost.cs:18)

同时，软键盘只接受当前 `PART_Candidates` 对应的 `SoftKeyboardCandidateList` 发出的候选事件。若外部想换成多行网格、独立候选弹层或另一种列表控件，就缺少公开的提交、翻页命令来连接现有逻辑。[相关代码](E:/Repos/WPF/wpfui/src/Wpf.Zen.Ui.VirtualInput/Controls/SoftKeyboard.Candidates.cs:135)

我建议接下来优先补齐三类扩展契约：

1. **候选会话与适配策略**：允许外部替换候选来源，也允许只覆盖某个输入法的提交、翻页或接管策略，其余行为继续委托默认实现。
2. **附加布局注册机制**：把适用条件、标识、名称、首选规则和布局提供器连接起来，自动接入设置与选择记忆。
3. **公开操作入口与呈现槽位**：自定义界面可以通过命令提交候选、翻页、切换输入法；候选面板和模板可以独立替换。

其中，焦点校验、会话归属、过期请求拒绝和资源释放，仍应由控件统一保证，避免每个适配器重复实现。

**最有价值的验收标准是：把小狼毫适配移到一个独立的外部程序集后，不修改核心库，仍能完成候选读取、数字提交、原生浮窗策略和自定义设置。** 当前架构还做不到这一点；补齐后，才有条件把不同输入法的兼容工作交给外部扩展，而不是持续往核心控件里增加厂商判断。
