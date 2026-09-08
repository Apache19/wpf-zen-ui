# SoftKeyboard

`SoftKeyboard` 是可参与 WPF 布局、保持编辑器焦点的软键盘控件，适合平板、Kiosk、POS
和需要让键盘稳定占据 Page、ContentDialog 或表单指定区域的应用。

## 基本用法

```xaml
<Grid x:Name="FormScope">
    <Grid.RowDefinitions>
        <RowDefinition Height="*" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <StackPanel>
        <ui:TextBox PlaceholderText="姓名" />
        <ui:PasswordBox PlaceholderText="密码" />
    </StackPanel>

    <ui:SoftKeyboard
        Grid.Row="1"
        AutoShowMode="TouchAndPen"
        IsOpen="{Binding IsKeyboardOpen}"
        TargetScope="{Binding ElementName=FormScope}" />
</Grid>
```

键盘及默认键帽均不可获得键盘焦点。每次输入前都会重新验证目标、作用域、前台 HWND、
启用/只读状态和焦点。普通键通过虚拟键进入 WPF 原有的命令、选区、撤销、死键和 TSF/IME
管线；Emoji 和不能映射为虚拟键的 Unicode 序列使用 Unicode `SendInput`。

## 目标与自动开关

- 设置 `Target` 可锁定单个编辑器；留空则跟随 `TargetScope` 内最后聚焦的受支持编辑器。
- `TargetScope` 为空时使用所在 `Window`。
- `AutoShowMode="TouchAndPen"` 仅响应触摸或笔引发的焦点变化；`AnyFocus` 响应任意焦点来源。
- `AutoHideMode="FocusLeavesScope"` 在焦点离开表单或对话框区域时关闭；`Manual` 由应用控制。

内置适配器支持 WPF `TextBox`、`RichTextBox`、`PasswordBox` 及其库内派生控件。其它 WPF
编辑器可实现 `ISoftKeyboardTargetAdapter`；行业专用布局可实现
`ISoftKeyboardLayoutProvider` 并返回不可变布局模型。

## 布局与页面导航

默认的 DisplayMode 为 Automatic，按当前编辑器的 InputScope 自动选择字母或数字键面。
ShowDisplayMode(Letters)、ShowDisplayMode(Symbols) 等显式请求优先于输入作用域；
因此数字键面的 ABC 可正常返回字母。切换到另一个编辑器时重新使用 Automatic。

公共工具栏始终提供独立的 ← ABC 返回键与关闭键，包括 Emoji 和输入法选择页面。
← ABC 返回键只切换键面，不改变 IME 状态；IME 切换按钮单独控制输入模式。
EmojiSearchText 的 null 绑定结果会强制为空字符串，表示不限制搜索。

## IME 候选与输入法

默认模板底部标为 `ABC` / `IME` 的按钮是输入模式开关：输入法打开时点击会切到直接字母输入，再次点击会恢复
当前输入法；切换不会把键盘焦点移出编辑器。如果当前是普通英文键盘布局，控件会先激活本次
会话上次使用的 TIP；首次使用则优先选择与应用 UI 区域设置匹配的已启用 TIP。微软拼音的
“中/英”状态同时受 TSF open 和 conversion compartment 控制，控件会一起更新，避免出现属性
显示已开启但仍直接输入字母的情况。切换只改变本地语言/直接字母标志，保留输入法当前的全角、
半角、罗马字等用户偏好，不会替输入法强制选择字形模式。当前组合状态由只读 `ImeState` 属性公开。符号键盘中的
`ABC` 键只负责返回字母键面，两者语义不同。

控件在 WPF 首次异步注册 TextStore 前，使用同一 Dispatcher 的 TSF ThreadManager 声明
`TF_TMAE_UIELEMENTENABLEDONLY`，并为自己的激活严格配对 `Deactivate`；WPF 仍创建和拥有唯一
的 TextStore、DocumentManager 与 client id。TextStore 已存在时，控件通过缓存且可降级的
反射读取 WPF 的 `TextServicesHost.ThreadManager`，不会替换或重建 WPF 文本管线。
仅在键盘打开、候选属于 TSF 当前焦点文档且输入法实现
`ITfCandidateListUIElementBehavior` 时接管候选；点击候选会调用 `SetSelection` 与 `Finalize`。
若 TIP 在 sink 订阅前已打开候选，控件还会从同一个 `ITfUIElementMgr` 发现该候选，并仅在候选
存续期间短时同步其内容。微软拼音支持这组标准 UIElement 接口，可使用内嵌候选栏。

只有 `SoftKeyboard` 已打开且实际可见时才隐藏输入法的原生候选窗。关闭键盘，或折叠键盘及其
任意祖先（例如 Page、ContentDialog 的键盘区域）时，会先恢复原生候选窗再释放内嵌候选；若
组合输入仍在继续，键盘重新显示后会立即发现同一个 TSF 候选并恢复内嵌呈现，无需再按一次键。

不支持可控制候选的输入法继续显示原生候选窗。可通过 `CandidatePresentationState` 区分
`Embedded`、`NativeFallback` 和 `Unavailable`；当前分页由 `CandidatePageNumber` 与
`CandidatePageCount` 公开。输入法面板使用公开 TSF profile API 枚举和切换已启用 profile，
不调用系统语言切换浮层；COM 不可用时退回 WPF 输入语言列表。

候选栏的视觉、分页、点击上屏均由控件内置，但候选词内容来自用户当前选择的 Windows 输入法。
控件不会另带拼音词库或记录用户输入，这样可以继续使用系统输入法的词频、专业词库和隐私设置。
候选栏同时提供不可聚焦的上一页、下一页触摸按钮；按钮按输入法通过 TSF 声明的分页边界移动
选择，但不会提交候选，首尾页会自动禁用无效方向。
输入法已开启且当前字母键面支持内嵌候选时，候选栏会保留固定的最小高度；暂时没有候选只会
隐藏页码和翻页按钮，不会让键区上下跳动。IME 关闭、没有活动编辑目标、进入 Emoji/输入法选择
界面，或输入法只能使用原生候选窗时，候选栏才整体折叠。

候选接管要求目标仍在作用域内、可编辑并保持键盘焦点，宿主仍在前台，且当前键面支持显示候选。
切换到 Emoji 或输入法选择页、禁用键盘、离开目标作用域时会释放内嵌候选并尝试恢复原生候选窗。
读取、翻页和提交前还会核对 TSF 当前焦点文档，不能借用其他编辑器的候选。

候选展示与 TSF 组合文本是两个独立生命周期。控件通过 WPF 文本组合事件跟踪当前编辑器的预编辑
事务；切换键面、切换输入法、关闭键盘或插入 Emoji、符号和长按替代字符等直接 Unicode 文本前，
会先向仍保持焦点的输入法发送 Esc。这样由输入法按自身语义删除尚未确认的组合文本，而不是使用
可能保留拼音字母的 Enter，也不会用仅结束组合标记、但可能留下组合范围文字的强制终止方式。
控件仅在目标仍有键盘焦点且窗口位于前台时发送取消键，避免把 Esc 泄漏给其他编辑器或应用。

## 系统键盘与兼容性

只有控件已加载、实际可见、`IsOpen=true`、宿主窗口在前台，而且有效编辑目标仍保持键盘焦点时，
控件才使用 Windows 10 1607+ 的窗口级 `IInputPaneInterop` / `IInputPane2.TryHide` 请求隐藏系统
输入面板。Windows 可以拒绝该协商请求；控件不会查找、移动或终止 `TabTip` 窗口。结果由
`SystemKeyboardSuppressionState` 公开，窗口重新激活、编辑器焦点变化和后续输入时会以节流方式
重新请求隐藏。

关闭或折叠控件、折叠其任意祖先、目标失效或焦点离开目标后，控件立即结束自己的抑制状态。
仅当本控件此前的 `TryHide` 确实被 Windows 接受、原编辑器仍有效并保持焦点、窗口仍在前台，且
同一 Dispatcher 没有另一个可见软键盘仍需抑制时，才调用一次 `IInputPane2.TryShow` 尝试恢复
系统键盘；最终是否显示仍由 Windows 根据物理键盘、用户设置和输入策略决定。窗口失活或控件
卸载时不会发出显示请求，避免把系统键盘带到正在关闭或已经切换的窗口。

库继续提供 `net472` 和 `net8.0-windows` 构建。旧系统以及 WinRT、COM 或 WPF 内部成员不兼容
时只关闭对应增强能力，不影响基础输入和应用稳定性。

## Emoji、样式与可访问性

Emoji 模型包含分类、可搜索名称和肤色变体；`EmojiCategory` 与 `EmojiSearchText` 可用于过滤。
最近使用项只保留在当前控件实例内，不持久化、不传输。字符和 Emoji 长按均可显示变体。

默认模板支持 Light、Dark、High Contrast、DPI 和触摸命中尺寸，约 320 DIP 起可用，并在宽屏
限制键区宽度。可通过 `{ui:DynamicStyle UiSoftKeyboard}` 或
`StyleKeys.UiSoftKeyboardStyle` 派生样式。控件向 UI Automation 暴露 Expand/Collapse，键帽
使用具备名称和 Invoke 行为的标准按钮 peer，同时始终保持不可聚焦。

长按在键盘禁用、隐藏、卸载、窗口失活或鼠标捕获中断时结束；普通松手仍保留正确的单击抑制行为。
