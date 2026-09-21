# SoftKeyboard

`SoftKeyboard` 是可参与 WPF 布局、保持编辑器焦点的软键盘控件，适合平板、电子触控互动设备、POS
和需要让键盘稳定占据 Page、ContentDialog 或表单指定区域的应用。

软键盘核心位于 `Wpf.Zen.Ui.VirtualInput` 程序集。中文和日语适配位于单向依赖
核心的 `Wpf.Zen.Ui.VirtualInput.Extensions` 程序集。核心不扫描程序集、不执行模块初始化，也不维护
进程级扩展注册表；每个 `SoftKeyboard` 通过 `ExtensionProvider` 显式选择自己的扩展组合。
XAML 使用专属 XML 命名空间，C# API 按职责分布在 `.Controls`、`.Candidates`、`.Extensions`、
`.InputMethods`、`.Input`、`.Layouts` 与 `.Theming` 中。

扩展库按扩展入口、输入法适配、布局、候选策略和语言路由划分目录。职责与依赖说明见
[扩展库结构](../src/Wpf.Zen.Ui.VirtualInput.Extensions/README.md)。

## 基本用法

```xaml
<Window
    xmlns:ui="http://schemas.thezendev.com/wpf/2023/xaml"
    xmlns:keyboard="http://schemas.thezendev.com/wpf/2027/virtual-input">
<Grid x:Name="FormScope">
    <Grid.RowDefinitions>
        <RowDefinition Height="*" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <StackPanel>
        <ui:TextBox PlaceholderText="姓名" />
        <ui:PasswordBox PlaceholderText="密码" />
    </StackPanel>

    <keyboard:SoftKeyboard
        Grid.Row="1"
        Visibility="{Binding KeyboardVisibility}"
        TargetScope="{Binding ElementName=FormScope}" />
</Grid>
</Window>
```

上例只引用核心包，提供标准字母、网址、邮箱、数字和通用符号布局，以及 Windows 输入法枚举和
通用 TSF 候选能力。要启用本仓库提供的全部输入法适配，可引用扩展包并显式设置默认组合：

```xaml
<Window
    xmlns:keyboard="http://schemas.thezendev.com/wpf/2027/virtual-input"
    xmlns:keyboardExtensions="http://schemas.thezendev.com/wpf/2027/virtual-input/extensions">
    <keyboard:SoftKeyboard
        ExtensionProvider="{x:Static keyboardExtensions:DefaultSoftKeyboardExtensionProvider.Default}" />
</Window>
```

也可以只组合应用需要的扩展。声明顺序就是每类能力的优先级，且作用域仅限当前控件：

```csharp
keyboard.ExtensionProvider = new CompositeSoftKeyboardExtensionProvider(
    ChineseSoftKeyboardExtensionProvider.Default,
    JapaneseSoftKeyboardExtensionProvider.Default
);
```

扩展能力按布局、附加布局、输入模式和 TSF 候选行为分别解析。显式 `LayoutProvider` 和
显式 `InputModeAdapter` 优先于扩展提供的适配器；按钮按语言栏与显式覆盖规则解析；候选行为采用第一个返回已处理结果的策略；附加布局聚合全部匹配项，
重复 ID 保留首项并写入 `ExtensionError`。扩展异常会被隔离并安全降级。

键盘及默认键帽均不可获得键盘焦点。每次输入前都会重新验证目标、作用域、前台 HWND、
启用/只读状态和焦点。普通键通过虚拟键进入 WPF 原有的命令、选区、撤销、死键和 TSF/IME
管线；普通符号和长按变体使用 Unicode `SendInput`。
默认不包含搜索或面板。

## TSF-aware 行为契约

控件是应用内的 TSF-aware 输入表面：WPF 持有文本存储、组合事务和线程管理器，软键盘负责
注入按键、观察状态和按能力协商候选展示。它沿用 WPF 的正常 TSF 激活策略；输入法没有提供
可控制候选接口时继续显示原生窗口。TSF-aware 不等于强制 UI-less，也不保证所有 TIP 都能内嵌候选。

- 输入法菜单通过 `ITfInputProcessorProfileMgr.EnumProfiles` 同时枚举启用的 TIP 和键盘布局，
  普通键盘还需存在于 `GetKeyboardLayoutList` 的实际加载列表，排除未加载及被 TIP 替代的布局。
  枚举失败时不再将语言配置合成为输入法条目。同语言的不同布局在内部通过精确 HKL 区分和激活；
  该句柄只用于当前 Windows 会话，不应持久化。应用从 `SoftKeyboard.InputMethods` 选择条目，
  将该 `SoftKeyboardInputMethod` 实例传给 `TryActivateInputMethod` 即可激活对应输入法或布局。
- 菜单第一行显示按当前 UI 语言本地化的语言和地区，第二行显示输入法或键盘名称。
  同语言下的多个 profile 分别呈现，命令参数仍是各自的完整 profile。
- 候选开始通知提供真实元素 ID；枚举发现的候选没有 ID，读取、翻页、提交和轮询时必须确认其
  COM 身份仍在 TSF 管理器的活动列表中。文档仍聚焦、COM 对象仍可调用，都不等于候选仍有效。
- 已内嵌候选每 75ms 补查一次，以兼容缺失更新通知的输入法。补查和候选结束后的异步核对使用
  `Normal` 优先级，避免持续输入推迟候选更新；候选失效或会话释放后停止补查。
- `GetSelection` 返回 `S_FALSE` 时公开 `SelectedCandidateIndex=null`；没有选中项不影响候选展示。
- 普通符号和长按字面文本先请求 Escape，等待 WPF 组合完成后才注入 Unicode。到本轮
  `ContextIdle` 仍未结束组合、出现新组合或输入会话发生变化时，放弃本次文字，后续不自动重放。
- 自定义同步动作的 `TrySendText` 遇到活动组合时只请求取消并返回 false；需要完整等待的扩展应
  实现 `IAsyncSoftKeyboardKeyAction` 或 `IAsyncSoftKeyboardAlternateKeyAction`，并 await
  `context.TrySendTextAsync(text)`。动作取消或上下文过期后，待发送文本不会进入后续编辑器。

输入法的专有模式通过扩展适配。兼容性验证以具体 Windows 和输入法版本为范围，记录见
[TSF 检查记录](SoftKeyboard.Tsf.md)。

## 显示控制与输入目标

- 设置 `Target` 可锁定单个编辑器；留空则跟随 `TargetScope` 内最后聚焦的受支持编辑器。
- `TargetScope` 为空时使用所在 `Window`；在 WinForms `ElementHost` 或独立
  `HwndSource` 中则使用当前呈现源的根视觉元素。前台安全校验、InputPane 请求与输入法消息
  都使用编辑器所属的 HWND，不要求 WPF `Window`。
- 显示状态由应用通过标准 WPF `Visibility`、样式触发器或父容器控制，默认值为 `Visible`。
  上例的 `KeyboardVisibility` 是视图模型上的 `Visibility` 属性；布尔状态可配合
  `BooleanToVisibilityConverter` 使用。
- `Collapsed` 隐藏并释放布局空间；`Hidden` 保留布局空间。父容器隐藏同样会暂停输入活动。
- 焦点变化只更新输入目标和输入法状态，显隐策略（包括触摸唤起、失焦隐藏）由页面或宿主实现。
  宿主显示按钮宜设置 `Focusable="False"`，以保留编辑器焦点。
- 控件以只读 `IsVisible` 管理实际可见期间的工作。隐藏会取消按键手势和异步输入、
  关闭浮层、释放候选展示及系统键盘抑制；重新显示时按当前目标和输入法状态刷新。
  取消组合输入仍需原编辑器持有前台焦点，遵循本文的组合输入安全约束。
- 构造时不生成布局或读取原生输入状态。应用模板时会生成布局以支持 WPF 测量，
  包括首次以 `Hidden` 加载时所需的占位；原生观察仍以已加载、实际可见且启用为前提。
  需要提前查询时调用 `RefreshLayout()` 或 `RefreshInputMethods()`。

内置适配器支持 WPF `TextBox`、`RichTextBox`、`PasswordBox` 及其库内派生控件。其它 WPF
编辑器可实现 `ISoftKeyboardTargetAdapter`；行业专用布局可实现
`ISoftKeyboardLayoutProvider` 并返回不可变布局模型。

## 布局生成与扩展

标准布局使用四行；附加语言面板可以采用自己的行列结构。标准字母行采用十键首行、错位九键次行及带 Shift/Backspace 的第三行；
底行包含符号切换、输入模式、逗号、左右方向、空格、句号、输入法菜单和 Enter。
符号面提供 `Symbols` / `MoreSymbols` 两页。核心只提供通用符号；中文规则由扩展根据 IME 状态贡献，
日语规则由语言扩展注册，韩文及未知语言不会套用中文标点。
数字键面把命令安排在右侧，也保持四行。日语主页面独立使用六行物理键布局，数字和 OEM 符号键
常驻，并显示字母／符号与假名双标注；长音、中点等由对应物理键及 Shift 交给 IME 处理。
符号面也保留地球菜单键，以便在任何默认布局中直接选择输入法。

`Automatic` 根据当前编辑器的 `InputScope` 选择字母、网址、邮箱或数字布局。
`InputScope="Url"` 的字母底行为 `123 中 http : ␣ .com . / 切 ↵`，空格与普通键等宽；
`InputScope="EmailSmtpAddress"` 的字母底行为 `123 中 @ ␣ .com . - 切 ↵`。
这里的“中”代表可选模式按钮，实际采用语言栏内容或扩展键帽，“切”使用地球图标打开输入法菜单，`123` 打开数字符号页。
`http` 按字面输入，长按选择 `https`，不会自动追加 `://`；`.com` 长按提供
`.net`、`.org`、`.edu`、`.gov`，以及输入语言对应的一个地区域名（例如 `.cn`、`.jp`、`.uk`）。
句号键同时显示主内容 `.` 和变体提示 `?`，长按输入 ASCII 问号。
地址快捷键始终输入半角字面文本，不随 Shift 或 IME 模式改为全角标点。
显式 `Letters` 仍按网址/邮箱作用域提供快捷键，因此从符号页返回时保留专用布局；
显式 `Numbers`、`Symbols`、`MoreSymbols` 优先于输入作用域。更换编辑器时恢复 `Automatic`。
切换输入模式保留当前符号页，不会自动跳回字母页。隐藏由应用设置 `Visibility=Collapsed` 或 `Hidden`；
默认模板没有额外工具栏、输入法选择页或关闭按钮。

`ISoftKeyboardLayoutProvider.CreateLayout` 只需处理自己负责的上下文，不负责时返回 `null`。
`CompositeSoftKeyboardLayoutProvider` 按声明顺序查询，首个非 null 布局胜出，空布局也是有效结果。
建议依次注册特定 TSF profile、特定地区/语言、通用默认提供器。应用显式布局异常写入
`LayoutError`；扩展布局异常写入 `ExtensionError`，随后继续安全降级。
`SoftKeyboardLayoutRule` 将匹配条件与构建函数组合成一个小提供器，适合只扩展一个页面或一种输入法。

上下文包含只读 `Culture`、`InputScope`、字符串 `Page`、`ShiftState`、`InputMethod`、
独立的 `InputModeButton` 和独立于 Page 的 `SupplementalLayout`。
自定义提供器可通过 `context.GetKeyLabel(Key.A)` 获取键帽文字；该方法使用上下文创建时捕获的
Windows 键盘布局和虚拟 Shift 状态，句柄由内部管理。`Culture` 描述输入语言，不负责选择键盘布局。
`InputMethod` 提供 ClassId、ProfileId、LanguageId 和可空的 `TouchLayoutPreference`，
可精确匹配输入法厂商与 profile。默认键帽也通过 `GetKeyLabel` 解码，不应把显示文字当成输入法组合文本。
提供器不得在构建中更改焦点、输入语言或控件状态，也不要缓存编辑器；应用自己的数据变化后调用 `RefreshLayout()`。

`SoftKeyboardPages` 提供内置页面常量，页面集合不是封闭枚举。自定义页面使用如 `sample:kaomoji` 的
有命名空间的字符串，通过 `ShowPage()` 或 `SoftKeyboardKey.Navigate()` 导航。自定义页面同样遵守
组合取消和切换编辑器时恢复 `Automatic` 的规则；切换 Shift、输入模式或 profile 会重新解析当前页面。

```csharp
keyboard.LayoutProvider = new CompositeSoftKeyboardLayoutProvider(
    new SoftKeyboardLayoutRule(
        context => context.Culture.TwoLetterISOLanguageName == "ja"
            && context.Page == "sample:kaomoji",
        context => new SoftKeyboardLayoutBuilder(6)
            .AddRow(0, new[]
            {
                SoftKeyboardKey.FromText("(^_^)"),
                SoftKeyboardKey.FromText("(T_T)"),
                SoftKeyboardKey.FromText("m(_ _)m"),
            }, columnSpan: 2)
            .AddRow(1, new[]
            {
                SoftKeyboardKey.Navigate(SoftKeyboardPages.Letters, "ABC", "返回字母"),
                SoftKeyboardKey.Navigate(SoftKeyboardPages.Symbols, "123", "返回符号"),
            }, new[] { 3, 3 })
            .Build()),
    DefaultSoftKeyboardLayoutProvider.Default);

// 在日语输入环境中打开；其他环境不匹配时恢复内置 Automatic 页面。
keyboard.ShowPage("sample:kaomoji");
```

控件在自定义提供器返回 null 后尝试内置提供器。仍无匹配时，`Page` 与布局一起恢复到 `Automatic`，
避免显示字母但仍处在不存在的页面。`LayoutError` 暴露未匹配页面或提供器异常；下一次成功解析后清空。
因此只实现一小块布局即可使用控件，无需复制默认实现或写异常回退代码。

默认实现的职责划分如下：

| 部分 | 职责 |
| --- | --- |
| DefaultSoftKeyboardLayoutProvider | 核心通用回退，委托 Standard；语言专用策略由显式扩展提供 |
| Japanese / ChineseCompactLayoutProvider | 由适配器按观察选择，分别定义日文、中文符号表；共享场景路由遵守 InputScope 和页面约束 |
| StandardSoftKeyboardLayoutProvider | 可独立复用的通用布局组合，不是语言实现必须继承的基类 |
| Url / Email / NumberSoftKeyboardLayoutProvider | 各自负责完整键盘页的匹配、全部键位和网格结构 |
| Letter / SymbolKeyboardLayout | 可选的紧凑布局生成函数，不承担语言或地址场景选择 |
| CompactKeyboardRepertoire | 上述生成函数可选的符号素材，不是语言提供器的接口或约束 |
| PhysicalKeyboardKeys / AddressKeyboardKeys | 可选的未定位字母键、地址字面快捷键与地区域名；不拥有页面结构 |
| KeyboardLayoutLabels | Windows HKL 键帽解析，不改变死键状态 |
| SoftKeyboardLayoutBuilder | 行、错位、宽度换算及不可变快照 |
| SoftKeyboard | 目标校验、页面与 Shift 状态、输入和组合生命周期 |
| KeyControl / Panel / XAML | 按键交互、测量排列、视觉呈现 |

### 完整接管地址场景或某种语言

`Culture` / `InputMethod` 表示输入环境，`InputScope` 表示编辑场景，`Page` 表示场景中的当前页面。
它们是独立匹配条件，没有“语言只能换符号”或“URL 只能换底行”的限制，也没有由框架隐式决定的匹配优先级。
组合顺序就是明确的优先级，匹配后返回的布局就是最终完整快照，不会再追加默认字母行或功能栏。

例如，下面的方法接收已经实现的独立提供器，将日语的 URL、邮箱、符号、主输入页面分别接管：

```csharp
static ISoftKeyboardLayoutProvider ComposeJapanese(
    ISoftKeyboardLayoutProvider urlLayouts,
    ISoftKeyboardLayoutProvider emailLayouts,
    ISoftKeyboardLayoutProvider symbolLayouts,
    ISoftKeyboardLayoutProvider inputLayouts)
{
    var japanese = new CompositeSoftKeyboardLayoutProvider(
        new SoftKeyboardLayoutRule(
            c => c.InputScope == InputScopeNameValue.Url,
            urlLayouts.CreateLayout),
        new SoftKeyboardLayoutRule(
            c => c.InputScope == InputScopeNameValue.EmailSmtpAddress,
            emailLayouts.CreateLayout),
        new SoftKeyboardLayoutRule(
            c => c.Page is SoftKeyboardPages.Symbols or SoftKeyboardPages.MoreSymbols,
            symbolLayouts.CreateLayout),
        new SoftKeyboardLayoutRule(
            c => c.Page is SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters,
            inputLayouts.CreateLayout),
        JapaneseSoftKeyboardLayoutProvider.Default);

    return new CompositeSoftKeyboardLayoutProvider(
        new SoftKeyboardLayoutRule(
            c => c.Culture.TwoLetterISOLanguageName == "ja",
            japanese.CreateLayout),
        DefaultSoftKeyboardLayoutProvider.Default);
}
```

这里 URL/邮箱规则没有限定 `Page`，因此它们收到该场景的所有页面请求，既可以提供自己的主输入页、
符号页和自定义工具页，也可以返回 null，把某一页委托给后续的日语规则。地址规则排在符号规则之前，
所以“日语 URL 符号页”优先由 `urlLayouts` 处理；如果希望所有场景共享日语符号页，将符号规则放在前面即可。
自定义页面同样在相应分支中注册，不受内置两页符号的限制。示例的主输入规则没有排除数字 InputScope，
因此也可以完整接管数字编辑器的 Automatic 页；是否委托默认数字页由 `inputLayouts` 自己决定。

如果希望跨所有语言接管 URL，则把作用域规则直接放到最外层：

```csharp
keyboard.LayoutProvider = new CompositeSoftKeyboardLayoutProvider(
    new SoftKeyboardLayoutRule(
        c => c.InputScope == InputScopeNameValue.Url,
        urlLayouts.CreateLayout),
    DefaultSoftKeyboardLayoutProvider.Default);
```

特定厂商输入法也采用相同组合方式：在语言分支之前加入匹配 `InputMethod.ClassId` 和 `ProfileId` 的规则。
嵌套组合始终传递原始上下文，保留输入场景、页面、按钮呈现和 profile；不用伪装成普通文本上下文来复用其他提供器。

内置 URL/邮箱提供器目前只接管自己场景中的 Automatic/Letters 整页，明确将其他页面交还给所在语言组合。
这是这两个内置实现的策略，不是地址场景的接口限制。它们分别组装所有四行，只复用未定位的字母键与少量字面快捷键。
即使默认页面恰好共享四行形状，也可以独立修改 URL 的首行、邮箱的命令键或整个网格，不需要修改普通字母布局。

日语扩展的 Automatic/Letters 主页面采用六行物理键布局：功能转换行、数字行、三行字母／OEM
符号键和底部编辑行。十个数字、26 个字母以及十二个 OEM 符号键同时可用，包括假名输入所需的
浊音、半浊音、长音、小假名和「ろ」对应键。字符键全部发送虚拟键，交由 IME 继续处理当前组合；
输入长音、标点或数字无需先切换到会取消组合的符号页。

主键帽与上方假名标注均按当前 Windows 键盘布局（HKL）及 Shift 状态翻译，采用字母／符号与
假名双标注。假名标注统一为便于阅读的平假名，实际输出的文字种类、宽度和输入方式由 IME 决定。
JIS 与英文键盘配置使用各自的 OEM 排列；原生布局不提供映射时保留键名，不伪造假名对应关系。
单次 Shift 在字母、数字或 OEM 字符键成功发送后释放，锁定 Shift 则保持；方向键和功能键不消耗它。

主页面还提供 Esc、Tab、Delete、四向箭头、変換、無変換及 F6–F10。
其中 F6–F10 的标注对应微软 IME 默认的平假名、全角片假名、半角片假名、全角英数和半角英数转换，
实际动作遵循输入法当前键位设置，参见 [微软日语 IME 快捷键说明](https://support.microsoft.com/en-us/windows/hardware/input-devices/microsoft-japanese-ime)。
输入模式按钮仍走现有观察和命令机制。关闭 IME、直接输入或暂未识别模式时也保留完整物理键布局。

日语扩展的独立「記号」页包含 `「」`、`『』`、`・`、`※`、`〒` 等字面文本，适合独立插入符号；
切页和字面文本插入仍遵守组合取消边界。网址、邮箱、数字场景继续使用各自的专用布局。
这些能力由 `JapaneseSoftKeyboardExtensionProvider` 注册，图库使用默认扩展集合即可获得。
布局创建日志使用 `[SoftKeyboard.Layout.Japanese]`，记录 HKL、OEM 排列及 Shift 状态。
六行页面需要比四行紧凑页面更多的垂直空间，宿主宜使用自动高度；默认键高下键区至少需要 332 DIP。
需要不同网格、更多页面或不同功能时，直接注册独立提供器，无需修改公共符号网格或核心控件。

普通键优先使用内置 `VirtualKey`、`Text`、`ChangePage` 等语义。特有功能实现
`ISoftKeyboardKeyAction` 或 `IAsyncSoftKeyboardKeyAction`，通过 `SoftKeyboardKey.FromAction()` 放入布局；
长按特有行为实现对应的同步或异步备选动作接口。动作获得短生命周期的
`SoftKeyboardActionContext`，可用 `TrySendKey()`、`TrySendText()`、`ShowPage()` 和 `RefreshLayout()`。
`TrySendKey` 接受 Shift、Ctrl、Alt 和 Windows 的 `ModifierKeys` 组合；输入层在部分失败时补发释放键。
异步上下文在任务完成前有效；新动作或输入会话失效会取消令牌并使旧上下文不能继续输入。
动作异常写入 `ExtensionError`，输入法模式解释与切换仍由 `InputModeAdapter` 负责。

运行时 `SoftKeyboardLayout.Keys` 是不可变的扁平集合，每键显式指定 `Row`、`Column`、
`ColumnSpan`。所有行共享 `ColumnCount`，短行不会被拉伸；跳过的 Row 编号不预留空行。
默认使用 20 个半键列，普通键占 2 列。`SoftKeyboardPanel` 统一负责测量和排列，
`RowSpacing` / `ColumnSpacing` 默认均为 4 DIP。布局提供器应避免重叠并保持各状态的行数一致。
极小容器下间距和键位会缩小；触摸使用应为键盘提供足够空间。

默认键区使用 `SoftKeyboardKeyList`，直接生成 `SoftKeyboardKeyControl` 容器，并在准备容器时应用
不可变布局的键位、内容和辅助名称。`SoftKeyboardPanel` 缓存行分组，尺寸或键内容变化不会重新分配行列表；
子元素、行号或折叠状态变化时才重建分组。备用字符弹窗的内容模板在首次打开时创建。
原生 `Grid` 的对比结果、测量方法及性能边界见 [布局性能评估](SoftKeyboard.LayoutPerformance.md)。

## 输入法观察、按钮和布局

软键盘不公开统一的“当前输入模式”。`ActiveInputMethod` 表示最后提交的 Profile，
`InputModeButton` 表示当前实际按钮呈现；二者都不保证输入法会接受下一次切换命令。
布局上下文的 `HasInputModeButton` 仅用于决定是否放置模式键。

状态展示跟随当前 Dispatcher 的输入上下文，不要求焦点进入 `TargetScope`，也不要求存在
`ActiveTarget`。首次加载、范围外焦点变化、窗口重新激活和可见性恢复都会刷新展示；
键盘隐藏时也可更新已提交状态。点击执行仍检查有效目标、焦点和原生命令的可用性。

首次启动时，系统可能已报告活动 Profile，但尚未提供语言栏项目和可解释的 TSF/IMM 状态。
对于 Windows 文本服务，此时模式键显示双向箭头图标，辅助名称为“切换输入模式（当前模式未知）”。
图标只表示操作入口；后续读到语言栏内容或可解释状态时自动替换。普通键盘布局没有 IME 模式，
不会显示这个未知状态图标。应用自定义的输入法源继续自行决定按键展示。

未知状态下的点击仍需有效输入目标。优先调用输入法自身的模式切换命令，由输入法决定切换结果；
若原生命令不可用，允许本次点击重新补读状态，再由扩展解析切换方向。补读后仍不能解析时，
本次点击不执行模式写入。原生命令执行失败不会再尝试扩展命令，避免一次点击走两条执行路径。
执行成功后仍以读取和通知结果更新显示，不预先显示推测的切换结果。

微软日语扩展按 `open=0` 显示直接字母数字输入；`open=1` 时解析 conversion，
基础字符模式由 `NATIVE`、`KATAKANA`、`FULLSHAPE` 三个位决定。`KATAKANA` 仅在
`NATIVE` 开启时有效；平假名不区分全／半角键帽。Roman 和 SoftKeyboard 标志不改变
字符模式，例如 `0x09`、`0x19`、`0x99` 均显示平假名“あ”。NoConversion、Fixed
保留基础字符显示，并在辅助说明中标出“不转换”或“固定转换”。字符代码、Hanja、EUDC、
符号等特殊模式以及未定义位不推测普通字符模式，回退平台呈现或中性图标。
扩展切换从关闭状态先写 `open=1`，再明确选择正常平假名转换，保留有效观察中的
Roman、SoftKeyboard 输入偏好并清除其它转换标志；
从已开启模式切换时只写 `open=0`，保留 conversion。两次开启写入分别报告结果，
任一步失败都返回失败，最终显示仍由实际状态通知决定。
只要已知 `open=1`，即使具体 conversion 尚不能解释，扩展仍可执行关闭；
此时不提供推测的字符模式显示。`open` 本身未知时仍需补读以确定方向。

上述规则复用 CsWin32 已生成的 `IME_CONVERSION_MODE`，不重复定义 Windows 常量。
标志含义参考 [IMM 定义](https://learn.microsoft.com/windows/win32/intl/ime-conversion-mode-values)
及 [TSF 定义](https://learn.microsoft.com/windows/win32/tsf/flags-for-conversion-mode)。
仅在扩展实际需要且 CsWin32 缺少定义时补充互操作声明。

### Profile 激活观察

原生监视器捕获 TSF Profile、open、conversion、语言栏通知和当前焦点 HWND 的 IMM 通知。
每次 open/conversion 通知在对应子系统读取两个状态值；扩展解释、语言栏查询和 UI 提交
在回调之外进行。WPF 的 `InputLanguageChanged` 补充输入语言变化通知，订阅不依赖编辑器
TextStore 是否已初始化。

键盘 Profile 激活后创建空观察记录，等待 **100ms 静默期，最长 500ms**。
open、conversion、语言栏相关通知会重置静默计时；普通按键和候选通知不延长窗口。
新 Profile、目标、实际焦点或来源替换会使旧观察过期。过期上下文重新发现并开启新观察；
卸载和 Dispatcher 关闭取消延迟任务。
计时到期后以 `DispatcherPriority.Normal` 执行观察结束和刷新，不等待鼠标、键盘输入队列清空。
500ms 是调度上限，UI 线程正在执行的工作仍可能推迟回调，不是输入法就绪保证。

观察期间保持已提交布局和按钮外观，忽略模式键点击，普通字符输入继续处理。
旧 Profile 的候选操作失效，不能借旧候选操作新输入法。观察结束才查询
`GUID_LBI_INPUTMODE`，解析扩展贡献，然后提交一次完整呈现并重建一次必要布局。
首次加载和编辑器目标变化也从空记录开始。显式刷新不绕过进行中的观察窗口。
稳定阶段的通知通过 Dispatcher 合并刷新；迟到通知继续更新，不做持续轮询。

`SoftKeyboardInputObservation` 是不可变的原始通知记录，不是同时读取的原子快照：

| 字段 | 含义 |
| --- | --- |
| `OpenObserved` / `ConversionObserved` | 本代是否收到过相应通知 |
| `OpenValue` / `ConversionValue` | 最后一次对应通知读取的整数；缺失或读取失败时为 null |

未通知不能解释为零、关闭或直接输入；读取失败不能继续使用该字段旧值。
观察结束后，每代可进行一次补读：先读 Dispatcher 的 TSF compartment，必要时再通过
当前焦点 HWND 的 IMM 上下文读取；尚无元素焦点时使用软键盘宿主 HWND。没有 HWND 也可
读取 TSF。扩展能够解释的结果才被接受，已有有效字段优先，不跨 Profile 或焦点上下文继承。
普通刷新不重复补读；用户点击时若缺少可执行的扩展切换描述，可额外补读一次。

### 扩展贡献与呈现覆盖

`ISoftKeyboardInputModeAdapter.GetContribution(profile, observation)` 只解释传入记录，
返回 `SoftKeyboardInputModeContribution`。可贡献 `Button`、`LayoutProvider`、
`Transition` 和默认 false 的 `OverridePlatformButton`，不输出通用模式或 `IsNativeInput`。
同一布局策略应复用提供器实例，避免无意义布局重建。

按钮解析顺序：

1. 扩展明确要求覆盖且提供有效按钮时，使用扩展内容。覆盖不承诺执行路径可用。
2. 否则使用当前服务有效的语言栏按钮；优先复制图标，无图标时可采用其有效文本。
3. 平台按钮不可用时，使用扩展的按钮与执行描述。
4. Windows 文本服务仍没有可用呈现时，显示未知状态的双向箭头图标。

语言栏项目的隐藏、禁用状态不阻止读取它的图标或文本用于展示；仍校验项目所属服务和 ID。
原生命令执行另外检查禁用状态和点击能力。

空覆盖不会屏蔽有效平台按钮。覆盖只改变外观，**不改变执行优先级**。
微软日文扩展使用这项能力，在观察足够时显示 `あ`、`カ`、`ｶ`、`Ａ` 或 `A`；
无法解释的缺项或未知值回退语言栏内容。中文扩展只解释明确识别的服务和已知值组合。

```csharp
public sealed class AppInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    private readonly Guid _serviceId;

    public AppInputModeAdapter(Guid serviceId)
    {
        _serviceId = serviceId;
    }

    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod profile, SoftKeyboardInputObservation observation)
    {
        // 本例只适用于应用已验证“open=0/1”契约的服务。
        if (profile.ClassId != _serviceId || observation.OpenValue is not (0 or 1))
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var open = observation.OpenValue == 1;
        return new SoftKeyboardInputModeContribution(
            new SoftKeyboardInputModeButton(open ? "app:on" : "app:off",
                open ? "开" : "关", "应用输入法切换"),
            new SoftKeyboardInputModeTransition(open ? "close" : "open"),
            overridePlatformButton: true);
    }

    public bool TrySwitchMode(SoftKeyboardInputMethod profile,
        SoftKeyboardInputObservation observation, SoftKeyboardInputModeTransition transition)
    {
        if (profile.ClassId != _serviceId || observation.OpenValue is not (0 or 1)
            || transition.Id is not ("open" or "close"))
        {
            return false;
        }

        return SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(
            profile, transition.Id == "open" ? 1 : 0);
    }
}
```

布局独立于按钮优先级：应用指定布局 → 已选附加布局 → 适配器贡献布局 → 普通扩展布局
→ 核心默认布局。即使显示语言栏按钮，扩展仍可根据观察选择布局。
提供器须遵守 InputScope 和页面约束；未知观察回落默认布局，不继承前一 Profile 的布局解释。
候选生命周期依据实际候选会话、文档、焦点和呈现策略，不依赖模式解释。

### 点击与短延迟刷新

点击先完成组合输入取消与目标准备，再确认实际 Profile，重新解析语言栏命令。
即使键帽被扩展覆盖，仍优先调用当前服务的语言栏按钮。原生调用校验服务归属、状态及
可调用 `OnClick` 的按钮样式，并使用实际屏幕位置与按钮区域。
样式与坐标约定见 [Microsoft OnClick 文档](https://learn.microsoft.com/en-us/windows/win32/api/ctfutb/nf-ctfutb-itflangbaritembutton-onclick)。

`SoftKeyboardInputModeCommandResult` 区分 `Unavailable`、`Accepted` 和 `Failed`。
只有命令不可用时才尝试扩展；原生命令一旦调用，即使失败或异常也不再执行第二条路径。
扩展接收最新观察和本次重新解析的执行描述，成功返回仅说明请求被接受。
服务专用执行校验可以使用必要的底层能力，但不得将写入目标或即时读值回填成通知观察。

每次执行尝试后安排 **一次 30ms 延迟刷新**，重新查询语言栏图标、文本和状态，再应用
扩展覆盖规则。提前收到通知不会取消这次复查；相邻请求合并，重复点击重置短延迟任务。
新 Profile 激活则由新的激活窗口接管。图标按内容标识比较，变化时更新实际键帽；
内容不变时保留现有布局。输入法若超过 30ms 才变化且始终不通知，无法保证自动跟随。
日文覆盖键帽不会被这次原生图标读取覆盖，只有可解释的后续观察改变其内容。

### 诊断与验证

结构化日志记录观察开始、原始通知、窗口提交或取消、按钮来源、覆盖选择及命令结果。
通知通道连接成功只表示已订阅，不能证明输入法会发通知。

确定性测试使用可控时钟验证 100ms/500ms 窗口、缺失及失败字段、过期取消和执行路径。
图标回归覆盖：已显示图标 A，点击后输入法无通知地改成 B，在 30ms 复查时更新按钮和键帽；
同时覆盖提前通知、迟到通知、相同图标和日文覆盖。
真实输入法测试需显式启用，并记录已安装版本；先验证 Profile 与语言栏图标可用，
只对确认会改变图标的转换验证变化，并允许 Dispatcher 调度余量，
不将“每次点击必然改变图标”作为通用契约。

## 常驻头部与长按浮层

头部显示候选和分页按钮；应用自行提供配置入口。
无候选或原生候选回退时，候选区域隐藏并仅显示当前输入法名称，不显示推断出的输入法模式；
整个头部不会折叠。

键帽由独立的 `SoftKeyboardKeyControl` 呈现，通过 `Key` 接收不可变的 `SoftKeyboardKey`。
布局提供器负责键帽内容、动作、`Alternates` 和 `IsRepeatEnabled`；键帽负责按下状态、
短按、重复、长按展开、变体选择和取消。键帽不持有编辑器、不读取 IME，也不调用 `SendInput`。

`SoftKeyboardKey.AlternateHint` 是可选的变体提示文字，默认 null；默认模板在主内容上方单独一行显示，
避免文字或图标重叠。提供器可只显示部分变体，例如 `.com` 的提示为 `.net`。
通过构造参数 `alternateHint: "?"` 配置提示，通过 `alternates: ["?"]` 配置实际长按列表；
提示仅负责展示，不参与输入或变体选择，空白提示自动隐藏。自定义模板可绑定 `Key.AlternateHint`。

长按有 `Alternates` 的键，在键帽自身模板的 `PART_AlternatePopup` 中展示变体。
可以移动到变体上松手，也可以松开原键后点击变体提交，
点击外部关闭；原键不会额外输入。浮层不参与键盘测量，隐藏、卸载、禁用和替换模板都会清理它。
Popup 使用不可聚焦按钮保留编辑器焦点，长按初始松手后才启用外部点击关闭，避免立即误关闭。
自定义键帽模板可以省略该可选部件，此时保留普通按键输入。变体按钮的 `DataContext` 是对应字符串。

键帽通过冒泡的 `InputRequested` 事件提交请求：`Key` 表示原按键，`AlternateText` 为 null
表示主动作，否则表示选中的变体字符串。动作实现 `ISoftKeyboardAlternateKeyAction` 时，变体由该动作处理；
其他变体继续走字面 Unicode 输入。`Invoke()` 和 UI Automation 也走同一请求入口。
键盘执行每次请求时仍校验目标、焦点和输入状态。键帽可单独使用，由宿主处理请求。

`InteractionStarted` 通知键盘取消其它键帽的交互；键盘隐藏、切换布局、目标变化或窗口失活时，
通过 `CancelInteraction()` 取消活动键帽。键盘不操作键帽的计时器、捕获和浮层。
键帽依据 WPF 提升后的鼠标事件支持鼠标、笔和主触点，当前采用单活动手势，不提供多指组合键。
重复默认关闭，由布局模型显式启用；内置布局仅为退格和方向键启用重复。
变体优先于重复，重复之后松手不会再触发主动作。重复频率与初始延时采用系统键盘设置。
退格不附加软键盘的一次性或锁定 Shift，以便微软拼音在组合输入期间收到普通 Backspace；
退格也不消耗 Shift 状态，后续字母仍按当前大小写状态输入。

## IME 候选与输入法

模式转换键的观察、覆盖和执行策略见上节。符号键盘中的 `ABC` 只返回字母键面，
与输入法模式命令无关。`ActiveInputMethod.IsActive` 只表示 Profile 被选中。

TSF 的初始化和激活由 WPF 的 TextStore 管线负责。控件通过缓存且可降级的反射读取
`TextServicesHost.ThreadManager`；首个编辑器尚未初始化时，等待后续焦点或输入刷新重试。
候选桥接不设置 `TF_TMAE_UIELEMENTENABLEDONLY`：WPF 已经拥有并激活线程管理器，事后修改激活
标志会改变当前线程允许加载的输入法。系统触摸键盘使用 UI-less 激活模式；公开 TSF 允许宿主在
`BeginUIElement` 中决定是否隐藏输入法 UI，但 Windows Shell 对内置输入法还有未公开的产品策略。
控件只在键盘打开、候选属于 TSF 当前焦点文档且实现
`ITfCandidateListUIElementBehavior` 时继续协商。`Wpf.Zen.Ui.VirtualInput.Extensions` 的默认策略
仅允许名单中明确列出的 Windows 内置东亚输入法使用内嵌候选，其余输入法保留原生候选窗口。
未配置扩展、扩展均返回 `Default` 或未提供候选行为时，也使用原生窗口。只有扩展明确返回
`Embedded` 才允许接管；标准提交调用 `SetSelection` 与 `Finalize`。
若 TIP 在 sink 订阅前已打开候选，控件还会从同一个 `ITfUIElementMgr` 发现该候选，并仅在候选
存续期间短时同步其内容。微软拼音支持这组集成候选接口，可使用内嵌候选栏。
枚举器仅用于当前同步查询，并在当前 Dispatcher 上及时释放，缩短该快照的原生引用寿命。

输入法菜单及菜单项均不可获得键盘焦点；选择时编辑器持续持有其 TSF 文档和输入法状态。
候选接管统一要求扩展明确准入，并检查接口能力、候选所属文档与当前焦点。
服务名单由扩展维护，核心不包含输入法厂商名单；回调发现和枚举发现遵循相同规则。
获取或枚举后未被接管的 UIElement 引用在当前同步查询结束前释放，不留给后续 GC 清理。
`BeginUIElement` 协商显示，首次 `UpdateUIElement` 才读取候选内容。
未提供可控制候选接口的输入法继续使用自己的浮窗。

候选展示以扩展显式准入为前提，并受接口能力和文档归属约束，不提供手动展示模式设置。

任何提交路径都以 TSF 后续通知决定候选显示，不因为 HRESULT 成功或按键入队就提前清空候选栏。

只有 `SoftKeyboard` 已加载且实际可见时才隐藏输入法的原生候选窗。隐藏键盘，或折叠键盘及其
任意祖先（例如 Page、ContentDialog 的键盘区域）时，会先尝试恢复原生候选窗再释放内嵌候选；若
组合输入仍在继续，键盘重新显示后可重新发现仍存续且符合策略的 TSF 候选。
原生恢复依赖 TIP 正确实现 Show。自动策略会从元素开始时协商是否接管，而非等到隐藏后才尝试补救。

`GUID_TFCAT_TIPCAP_UIELEMENTENABLED` 只声明基础 UIElement 能力，第三方输入法即使注册该类别，
也不一定支持完整的集成候选交互。`ITfIntegratableCandidateListUIElement` 是 Windows 文档为
IME 集成控件定义的可选契约，但声明该接口不能证明翻页、选词和提交生命周期完整兼容，
因此核心不据此自动嵌入。默认扩展维护允许嵌入的 TSF 服务名单；应用可通过
`ISoftKeyboardTsfCandidateBehavior` 为已验证的输入法显式返回 `Embedded`，其余返回
`Native` 或 `Default`。微信输入法由原生窗口负责展开、翻页、选词和复位。

不支持可控制候选的输入法继续显示原生候选窗。可通过 `CandidatePresentationState` 区分
`Embedded`、`NativeFallback` 和 `Unavailable`；当前分页由 `CandidatePageNumber` 与
`CandidatePageCount` 公开。地球键打开的 ContextMenu 使用公开 TSF profile API 枚举和切换已启用 profile，
不调用系统语言切换浮层；COM 不可用时退回 WPF 输入语言列表。

候选栏的视觉、分页、点击上屏均由控件内置，但候选词内容来自用户当前选择的 Windows 输入法。
控件不会另带拼音词库或记录用户输入，这样可以继续使用系统输入法的词频、专业词库和隐私设置。
候选栏同时提供不可聚焦的上一页、下一页触摸按钮；按钮按输入法通过 TSF 声明的分页边界移动
选择，但不会提交候选，首尾页会自动禁用无效方向。
候选接管要求目标仍在作用域内、可编辑并保持键盘焦点，宿主仍在前台。
禁用键盘、离开目标作用域时释放内嵌候选并尝试恢复原生候选窗。读取、翻页和提交前还会核对
TSF 当前焦点文档，不能借用其他编辑器的候选。

`CandidateSource` 可替换整套候选来源；默认值 `TsfSoftKeyboardCandidateSource.Default` 继续共享
Dispatcher 级 TSF 会话。核心统一负责目标校验、单次提交/翻页仲裁、取消、会话版本和过期结果拒绝。
自定义来源只能通过会过期的 `SoftKeyboardCandidateHost` 发布不可变快照：

```csharp
public sealed class AppCandidateSource : ISoftKeyboardCandidateSource
{
    public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
    {
        return new Session(host);
    }

    private sealed class Session(SoftKeyboardCandidateHost host)
        : ISoftKeyboardCandidateSession
    {
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            host.Publish(new SoftKeyboardCandidateSnapshot(
                [new SoftKeyboardCandidate(0, "应用候选")],
                SoftKeyboardCandidatePresentationState.Embedded));
            return Task.CompletedTask;
        }

        public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
            SoftKeyboardCandidate candidate,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.Success);
        }

        public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
            int delta,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
        }

        public void ReleasePresentation()
        {
        }

        public void Dispose()
        {
        }
    }
}

keyboard.CandidateSource = new AppCandidateSource();
```

`InputMethodSource` 按控件创建会话，在所属 Dispatcher 上同步提供目录和 Profile 激活。
可选的 `ISoftKeyboardInputModeButtonSession` 提供平台按钮查询和执行路由。
这些接口必须及时返回；依赖外部数据的来源自行维护缓存，读取缓存快照，不得在接口中等待网络、
后台任务或输入法未来事件。外部来源使用经过参数校验的 `SoftKeyboardInputMethod` 构造模型：

```csharp
public sealed class AppInputMethodSource : ISoftKeyboardInputMethodSource
{
    public ISoftKeyboardInputMethodSession CreateSession()
    {
        return new Session();
    }

    private sealed class Session : ISoftKeyboardInputMethodSession
    {
        private readonly SoftKeyboardInputMethod profile = new(
            Guid.Empty, Guid.Empty, 0x0409, "Application input", true);

        public SoftKeyboardInputMethodSnapshot GetSnapshot()
        {
            return new SoftKeyboardInputMethodSnapshot([profile], profile);
        }

        public bool TryActivate(SoftKeyboardInputMethod value)
        {
            return ReferenceEquals(value, profile);
        }

        public void Dispose()
        {
        }
    }
}

keyboard.InputMethodSource = new AppInputMethodSource();
bool refreshed = keyboard.RefreshInputMethods();
```

输入法激活使用 `TryActivateInputMethod(profile)`，只向系统发送 profile 激活请求，不写 open 或
conversion，也不根据返回值提前发布界面状态。请求失败或调用期间发生源、目标、生命周期变化时
返回 false；重入操作直接返回 false。返回 true 只表示原生请求被接受，后续状态由系统通知发布。

菜单选择会先关闭菜单，再同步发起激活；若有未完成的组合输入，则先等待取消确认。
激活后的通知合并和界面提交由观察期控制。

`InputMethods` 是只读依赖属性，每次发布完整的 `IReadOnlyList<SoftKeyboardInputMethod>` 快照。
使用属性绑定观察替换；已持有的旧列表保持不变。替换来源时立即清空旧目录。

候选刷新、自定义异步动作和候选确认仍支持真正的异步等待。新刷新替代旧刷新，新动作替代旧动作；
候选提交和翻页共享一个输入操作，繁忙期间拒绝第二个请求。取消立即撤销旧操作的结果、错误和刷新
发布权限，执行者在任务结束后释放自己的资源。隐藏、禁用、换目标、卸载和失去前台输入所有权
会使相应操作失效。输入法会话在替换来源或卸载时释放；同步回调内发生替换时，在旧调用退出后释放。

候选元素结束与操作取消是不同事件：元素结束立即禁止继续向旧元素输入，但可以作为已发送操作的
完成确认。调用者取消则不能再报告成功。扩展、候选和输入法错误分别见 `ExtensionError`、
`CandidateError`、`InputMethodError`；被取消或过期操作的迟到结果与异常不会覆盖当前状态。

候选提交、翻页和输入法激活可由任意自定义模板通过 `SoftKeyboardCommands.CommitCandidate`、
`MoveToPreviousCandidatePage`、`MoveToNextCandidatePage`、`ActivateInputMethod` 连接，无需命名模板部件。
`SoftKeyboardCandidateList` 是可派生的 `ItemsControl`，使用标准 `ItemsSource`、`ItemsPanel`、
`ItemTemplate` 与 `ItemContainerStyle`；默认横向 `StackPanel` 可换成 `WrapPanel` 或自定义面板。

同语言 TIP 切换由 TSF Profile 激活通知进入观察窗口；窗口结束和迟到通知按前述规则提交。
输入法菜单独立查询目录，不提前提交观察期间的按钮与布局。菜单项不参与键盘焦点；
没有有效编辑器持有焦点时仍
展示输入法列表，但禁用所有选择项。焦点离开原编辑器会使尚未执行的选择失效，不会重新聚焦
记忆中的目标。

候选展示与 TSF 组合文本是两个独立生命周期。控件通过 WPF 文本组合事件跟踪当前编辑器的预编辑
事务；切换键面、切换输入法、隐藏键盘或插入符号和长按替代字符等直接 Unicode 文本前，
会先向仍保持焦点的输入法发送 Esc。这样由输入法按自身语义删除尚未确认的组合文本，而不是使用
可能保留拼音字母的 Enter，也不会用仅结束组合标记、但可能留下组合范围文字的强制终止方式。
控件仅在目标仍有键盘焦点且窗口位于前台时发送取消键，避免把 Esc 泄漏给其他编辑器或应用。
等待组合取消后执行的输入法操作属于发起时的输入会话。目标更换、隐藏、禁用、卸载或窗口
失活会使待执行操作失效；恢复原状态也不会复活旧操作。执行前还会核对原目标是否仍保持焦点。

## 系统键盘与兼容性

只有控件已加载、实际可见、宿主窗口在前台，而且有效编辑目标仍保持键盘焦点时，
控件才使用 Windows 10 1607+ 的窗口级 `IInputPaneInterop` / `IInputPane2.TryHide` 请求隐藏系统
输入面板。Windows 可以拒绝该协商请求；控件不会查找、移动或终止 `TabTip` 窗口。结果由
`SystemKeyboardSuppressionState` 公开。焦点变化时除即时请求外，还会在本轮 WPF 焦点处理完成后
合并补发一次隐藏请求，避免 `UIElement.Focus()` 随后调用 `TipTsfHelper.Show()` 覆盖隐藏结果。
有效抑制期间每 250ms 复核目标和前台窗口并请求隐藏，处理 Win11 异步弹出系统键盘的情况；
定时复核使用 `Normal` 优先级，避免持续输入推迟隐藏请求。焦点变化后的补发请求仍使用
`Background`，以排在 WPF 可能通过 `Input` 调度的显示请求之后。
接口不可用时停止定时重试，待下一次焦点或显示状态变化再尝试。
整个过程不修改注册表、系统服务、全局触摸设置或 WPF 应用级开关，也不禁用编辑器 IME。
这属于自动隐藏而非 Windows 的强制禁止显示策略，系统拒绝请求时仍可能显示，异步弹出时也
可能短暂闪现；`Hidden` 表示隐藏请求被接受，不代表永久锁定系统键盘的可见性。

隐藏或折叠控件、折叠其任意祖先、目标失效或焦点离开目标后，控件立即结束自己的抑制状态。
仅当本控件此前的 `TryHide` 确实被 Windows 接受、原编辑器仍有效并保持焦点、窗口仍在前台，且
同一 Dispatcher 没有另一个可见软键盘仍需抑制时，才调用一次 `IInputPane2.TryShow` 尝试恢复
系统键盘；最终是否显示仍由 Windows 根据物理键盘、用户设置和输入策略决定。窗口失活或控件
卸载时不会发出显示请求，避免把系统键盘带到正在关闭或已经切换的窗口。

库继续提供 `net472` 和 `net8.0-windows` 构建。旧系统以及 WinRT、COM 或 WPF 内部成员不兼容
时只关闭对应增强能力，不影响基础输入和应用稳定性。

## 样式与可访问性

默认模板支持 Light、Dark、High Contrast、DPI 和触摸命中尺寸。键区填满提供的宽度；应用应根据触摸尺寸要求设置容器大小。
派生控件样式时，以 `SoftKeyboardStyleKeys.SoftKeyboardStyle` 为 `BasedOn` 资源键；该键属于
软键盘程序集，不会进入通用 UI 程序集的 `StyleKeys` 或 `DynamicStyle` 枚举。控件向 UI Automation 暴露 Expand/Collapse，键帽
使用专用的 `SoftKeyboardKeyControlAutomationPeer` 提供名称和 Invoke，同时默认保持不可聚焦。
键帽使用 `SoftKeyboardStyleKeys.KeyControlStyle` 派生样式，可独立替换键帽和变体浮层模板。

```xaml
<Style
    BasedOn="{StaticResource {x:Static keyboard:SoftKeyboardStyleKeys.SoftKeyboardStyle}}"
    TargetType="{x:Type keyboard:SoftKeyboard}">
    <!-- 应用级定制 -->
</Style>
```

`SoftKeyboardStyleKeys` 也通过软键盘 XML 命名空间公开，因此这里可以与控件共用 `keyboard` 前缀。

可直接设置 `Background`、`BorderBrush`、`BorderThickness`、`Padding`、`CornerRadius`、
`RowSpacing` 和 `ColumnSpacing`
定制外层表面，无需复制控件模板。默认内边距为 6 DIP、边框厚度为 1 DIP、圆角为 8 DIP；
背景和边框画刷继续随主题更新，显式设置属性时使用应用提供的值。

替换模板可复用 `SoftKeyboardPanel`，并在它的直接子元素上设置附加属性 `Row` 与
`Column`、`ColumnSpan`。有限宽度下键区按比例收缩；无限宽度测量（例如水平 StackPanel）下按最大单位
列宽需求预留空间，避免排列时普通键被较宽的功能键挤压。默认布局不使用状态相关的折叠键。

复用默认键区时可使用 `SoftKeyboardKeyList` 并绑定 `ItemsSource` 到 `CurrentLayout.Keys`，
为其 `ItemsPanel` 配置 `SoftKeyboardPanel`，并绑定 `ColumnCount` 和间距属性。
`ItemContainerStyle` 的目标类型是 `SoftKeyboardKeyControl`；`ItemTemplate` 定制的是
`SoftKeyboardKey.Content` 的呈现，模板的数据上下文就是该内容。
使用普通 `ItemsControl` 的自定义模板仍应把布局附加属性设置在其直接生成的容器上。

Debug 构建中，单轮面板测量达到 16 ms 时输出 `[SoftKeyboard.Layout] Slow MEASURE completed`。
日志分别记录容器生成准备（`generationMs`）、行分组检查或重建（`indexMs`）以及子元素测量
（`childrenMs`）的耗时；子元素测量包含按键模板创建。Release 构建不执行这段计时和日志逻辑。

自定义键区使用 `SoftKeyboardKeyControl` 并绑定 `Key`，键盘不把普通 Button 的 `DataContext`
解释为按键。候选及页面按钮仍由键盘处理；应用按钮若未使用候选模型或动作 Tag，其 Click 事件继续向宿主冒泡。
长按在键盘禁用、隐藏、卸载、替换模板、窗口失活或鼠标捕获中断时结束；普通松手仍保留正确的单击抑制行为。

## 验证

运行定向自动测试（项目使用 xUnit v3 进程内运行器）：

```powershell
dotnet build tests/Wpf.Zen.Ui.UnitTests/Wpf.Zen.Ui.UnitTests.csproj
& tests/Wpf.Zen.Ui.UnitTests/bin/Debug/net10.0-windows/Wpf.Zen.Ui.UnitTests.exe -class '*SoftKeyboard*'
```

当前 VirtualInput 测试结果为 504 项、0 失败、12 项原生环境测试跳过、2 项显式测试未运行。
自动测试覆盖核心无扩展、扩展组合和冲突、布局注册链路、异步来源版本隔离、异常降级、动作取消、
组合修饰键、候选列表扩展点，以及既有布局、菜单、候选、组合取消和目标隔离行为。
`GeneratedLetterKey_InvokeInsertsThroughWindowsInputStream` 和
`InstalledMicrosoftPinyin_PublishesCandidatesToEmbeddedSurfaceWhenAvailable` 是显式集成测试，
需要可激活前台窗口的交互桌面；后者还需要启用微软拼音。可在满足条件的桌面使用
`-explicit on -method '*测试方法名'` 单独运行，避免无人值守测试向用户桌面注入按键。

核心与扩展包可分别生成；扩展 nuspec 对两个目标框架都依赖同版本核心包，核心 nuspec 不依赖扩展：

```powershell
dotnet pack src/Wpf.Zen.Ui.VirtualInput/Wpf.Zen.Ui.VirtualInput.csproj -c Debug -o artifacts/packages
dotnet pack src/Wpf.Zen.Ui.VirtualInput.Extensions/Wpf.Zen.Ui.VirtualInput.Extensions.csproj `
    -c Debug -o artifacts/packages
```

## 初始化与按需发现

构造阶段不生成布局或读取输入法目录。`CurrentLayout` 初始为共享的 `SoftKeyboardLayout.Empty`
（无按键、一个逻辑列），`AvailableSupplementalLayouts` 初始为空；读取这两个属性不会隐式触发工作。
不可见状态下的布局、扩展、目标和页面配置变更只标记失效，保留上次生成的快照。
首次应用模板或显示时按最新配置生成布局；需提前查询布局或扩展目录的应用可以显式调用 `RefreshLayout()`。
已有布局的刷新仍等待当前输入法观察稳定；首次显示可先使用最后已提交的上下文生成基础表面。
按键路由事件在加载或首次显示时注册一次；Popup 属性描述器在实际连接按键 Popup 时初始化。
隐藏或禁用时暂停自动输入法观察；不可见状态的 Loaded 仅保留输入目标的焦点跟踪。
可用的 Windows 输入表面按需启动共享原生监听，最后一个表面停用时释放监听。
候选会话在实际使用或显式候选刷新时创建。

Windows 会话实现 `ISoftKeyboardActiveInputMethodSession`：普通刷新和输入前验证只读取活动配置，
不遍历全部语言和输入法。`InputMethods` 初始为空，仅通过 `RefreshInputMethods()` 或打开输入法
切换菜单请求完整目录；`ActiveInputMethod` 独立更新，目录中的 IsActive 表示上次枚举时的状态。
显式刷新可用于未加载/不可见的控件，目录立即发布，模式与键帽仍遵循原来的观察窗口。
自定义来源可实现该可选接口；仅实现基础会话接口时，仍通过 GetSnapshot 提供观察数据。

WPF 控件和 TSF 调用留在所属 Dispatcher，不通过 Task.Run 转移线程。
首次构造的分段测量、按需布局实现和复测结果见 [构造性能复核](SoftKeyboard.ConstructorPerformance.md)。

## 应用配置与附加布局

软键盘通过公开属性和布局选择 API 提供配置能力。控件库不提供设置菜单、菜单数据模型或设置提供器。
应用自行决定配置入口、文案、持久化和交互方式。

图库由页面上的复选框通过 `BooleanToVisibilityConverter` 绑定键盘的 `Visibility`。
点击外部 `DropDownButton` 时显式刷新布局目录并创建布局选择菜单。
菜单关闭后由页面恢复编辑器焦点，页面卸载时关闭菜单。

| API | 用途 |
| --- | --- |
| `Visibility` | WPF 标准显示状态，由应用或宿主控制 |
| `IsVisible` | WPF 只读实际可见性，包含祖先容器和呈现源的约束 |
| `AvailableSupplementalLayouts` | 上次生成的可用布局快照，可绑定；初始为空，显示或显式刷新后包含 Automatic、Standard 与适用的扩展布局 |
| `SelectSupplementalLayout(id)` / `SelectedSupplementalLayout` | 验证、选择并读取当前输入法的附加布局选择 |

可见期间布局目录随输入上下文和扩展变化更新；不可见期间按需刷新，已取得的快照不变。
应用执行延迟的选择前应检查该布局仍可用；`SelectSupplementalLayout` 会验证失效的目录，再安全应用选择。
选择布局仍会安全取消组合，并在控件生命周期内按输入法记忆；应用负责跨启动持久化。

附加布局通过 `ISoftKeyboardSupplementalLayoutProvider` 注册。每项把稳定 ID、显示名称、适用条件、
Automatic 首选规则和实际布局提供器放在同一不可变模型中，供应用发现布局，并统一用于校验、记忆和布局生成：

```csharp
public sealed class AppLayouts : ISoftKeyboardSupplementalLayoutProvider
{
    public IEnumerable<SoftKeyboardSupplementalLayout> GetLayouts(
        SoftKeyboardLayoutContext context)
    {
        return
        [
            new SoftKeyboardSupplementalLayout(
                "app:grid",
                "业务键盘",
                AppGridLayoutProvider.Default,
                isAutomaticPreferred: true,
                isApplicable: value => value.InputScope == InputScopeNameValue.Number)
        ];
    }
}

public sealed class AppExtensions : ISoftKeyboardExtensionProvider
{
    public IEnumerable<SoftKeyboardExtension> GetExtensions(
        SoftKeyboardExtensionContext context)
    {
        return [new SoftKeyboardExtension(supplementalLayouts: new AppLayouts())];
    }
}

keyboard.ExtensionProvider = new AppExtensions();
```

布局查询使用 `ITfFnGetPreferredTouchKeyboardLayout.GetLayout`，只获取布局类型及标识，不获取系统面板的几何和行为。
查询失败或输入法不实现接口时，`TouchLayoutPreference` 为 null；未知标识保留给自定义策略，不伪造系统支持。

微软日语扩展只解释本代已观察到的服务契约：已识别的 open/conversion 组合决定模式按钮文字。
完整日语物理布局由语言扩展注册，Romaji／Kana 使用同一套双标注键帽。
`OverridePlatformButton` 保留模式按钮文字覆盖能力；缺项或未知组合回退平台呈现。
点击优先重新发现语言栏命令；不可用时，扩展按观察决定关闭或显式选择平假名。
写入被接受不等于模式已改变，实际输入、组合与候选仍由当前输入法负责。

### 验证入口

普通测试直接运行编译后的 xUnit 测试程序，避免仓库现有 .NET 10 / VSTest 配置冲突：

```powershell
dotnet build tests/Wpf.Zen.Ui.UnitTests/Wpf.Zen.Ui.UnitTests.csproj -p:GeneratePackageOnBuild=false
& tests/Wpf.Zen.Ui.UnitTests/bin/Debug/net10.0-windows/Wpf.Zen.Ui.UnitTests.exe -class 'Wpf.Zen.Ui.UnitTests.VirtualInput.*'
```

`SoftKeyboardNativeImeTests` 是显式启用的前台测试，会在自己的 WPF 窗口使用本机已安装的输入法；
普通测试不会注入这些真实输入。启用方式：

```powershell
$env:WPF_ZEN_NATIVE_IME_TESTS = '1'
$runner = 'tests/Wpf.Zen.Ui.UnitTests/bin/Debug/net10.0-windows/Wpf.Zen.Ui.UnitTests.exe'
$imeCases = & $runner -class '*SoftKeyboardNativeImeTests' -preEnumerateTheories -list tests -noLogo -noColor
foreach ($imeCase in $imeCases) {
    if ($imeCase -like 'Wpf.Zen.Ui.UnitTests.*') {
        & $runner -preEnumerateTheories -displayName $imeCase -noLogo -noColor
    }
}
Remove-Item Env:WPF_ZEN_NATIVE_IME_TESTS
```

各原生用例独立进程执行，隔离输入法在文档关闭和配置切换后的状态。当前同进程连续切换用例
曾出现延迟输入进入后续编辑器的失败，不能将其结果当作独立会话的验证结果。

`Japanese_PhysicalSurfacePreservesRomajiAndKanaComposition` 按本机实际 Romaji／Kana 设置测试
已发布的键帽，覆盖组合中的 OEM 输入及 F7 转换；假名分支同时检查数字、浊音、小假名与长音。
`JapaneseSoftKeyboardLayoutTests` 检查完整键位、Shift、语言与页面路由，以及默认模板的六行几何。
可设置 `WPF_ZEN_LAYOUT_PREVIEW_DIRECTORY` 输出未按 Shift／已按 Shift 的 PNG，便于排版复核。

可控时钟回归覆盖观察合并、来源与目标失效、按钮及实际键帽更新、日文覆盖和命令回退。
真实语言栏图标测试要求当前服务确实提供按钮；例如 `GetItem` 返回 `S_FALSE` 且没有对象时，
该用例报告能力缺失并跳过，不能将模拟测试通过视为此 Windows/IME 版本的真实点击验证。
第三方输入法及其他 Windows/IME 版本需要各自验证。
