# SoftKeyboard

`SoftKeyboard` 是可参与 WPF 布局、保持编辑器焦点的软键盘控件，适合平板、电子触控互动设备、POS
和需要让键盘稳定占据 Page、ContentDialog 或表单指定区域的应用。

软键盘核心位于 `Wpf.Zen.Ui.VirtualInput` 程序集。中文和日语适配位于单向依赖
核心的 `Wpf.Zen.Ui.VirtualInput.Extensions` 程序集。核心不扫描程序集、不执行模块初始化，也不维护
进程级扩展注册表；每个 `SoftKeyboard` 通过 `ExtensionProvider` 显式选择自己的扩展组合。
XAML 使用专属 XML 命名空间，C# API 按职责分布在 `.Controls`、`.Candidates`、`.Extensions`、
`.InputMethods`、`.Input`、`.Layouts`、`.Settings` 与 `.Theming` 中。

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
        AutoShowMode="TouchAndPen"
        IsOpen="{Binding IsKeyboardOpen}"
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
`InputModeAdapter` 始终优先；候选行为采用第一个返回已处理结果的策略；附加布局聚合全部匹配项，
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
  枚举失败时不再将语言配置合成为输入法条目。同语言的不同布局以
  `SoftKeyboardInputMethod.KeyboardLayout` 区分，
  激活时传递精确 HKL；该句柄只用于当前 Windows 会话，不应持久化。
- 菜单第一行显示按当前 UI 语言本地化的语言和地区，第二行显示输入法或键盘名称。
  同语言下的多个 profile 分别呈现，命令参数仍是各自的完整 profile。
- 候选开始通知提供真实元素 ID；枚举发现的候选没有 ID，读取、翻页、提交和轮询时必须确认其
  COM 身份仍在 TSF 管理器的活动列表中。文档仍聚焦、COM 对象仍可调用，都不等于候选仍有效。
- `GetSelection` 返回 `S_FALSE` 时公开 `SelectedCandidateIndex=null`；没有选中项不影响候选展示。
- 普通符号和长按字面文本先请求 Escape，等待 WPF 组合完成后才注入 Unicode。到本轮
  `ContextIdle` 仍未结束组合、出现新组合或输入会话发生变化时，放弃本次文字，后续不自动重放。
- 自定义同步动作的 `TrySendText` 遇到活动组合时只请求取消并返回 false；需要完整等待的扩展应
  实现 `IAsyncSoftKeyboardKeyAction` 或 `IAsyncSoftKeyboardAlternateKeyAction`，并 await
  `context.TrySendTextAsync(text)`。动作取消或上下文过期后，待发送文本不会进入后续编辑器。

输入法的专有模式通过扩展适配。兼容性验证以具体 Windows 和输入法版本为范围，记录见
[TSF 检查记录](SoftKeyboard.Tsf.md)。

## 目标与自动开关

- 设置 `Target` 可锁定单个编辑器；留空则跟随 `TargetScope` 内最后聚焦的受支持编辑器。
- `TargetScope` 为空时使用所在 `Window`。
- `AutoShowMode="TouchAndPen"` 仅响应触摸或笔引发的焦点变化；`AnyFocus` 响应任意焦点来源。
- `AutoHideMode="FocusLeavesScope"` 在焦点离开表单或对话框区域时关闭；`Manual` 由应用控制。
- 打开、关闭策略独立，手动打开也可以配合自动关闭。

内置适配器支持 WPF `TextBox`、`RichTextBox`、`PasswordBox` 及其库内派生控件。其它 WPF
编辑器可实现 `ISoftKeyboardTargetAdapter`；行业专用布局可实现
`ISoftKeyboardLayoutProvider` 并返回不可变布局模型。

## 布局生成与扩展

标准布局使用四行；附加语言面板可以采用自己的行列结构。标准字母行采用十键首行、错位九键次行及带 Shift/Backspace 的第三行；
底行包含符号切换、输入模式、逗号、左右方向、空格、句号、输入法菜单和 Enter。
符号面提供 `Symbols` / `MoreSymbols` 两页。核心只提供通用符号；中文和日文符号规则由对应扩展
按语言及 IME 状态贡献，韩文及未知语言不会套用中文标点。
数字键面把命令安排在右侧，也保持四行。日文原生输入在第二行提供 `ー` 键，键帽提示并可长按选择 `・`。
符号面也保留地球菜单键，以便在任何默认布局中直接选择输入法。

`Automatic` 根据当前编辑器的 `InputScope` 选择字母、网址、邮箱或数字布局。
`InputScope="Url"` 的字母底行为 `123 中 http : ␣ .com . / 切 ↵`，空格与普通键等宽；
`InputScope="EmailSmtpAddress"` 的字母底行为 `123 中 @ ␣ .com . - 切 ↵`。
这里的“中”沿用当前输入模式标识，“切”使用地球图标打开输入法菜单，`123` 打开数字符号页。
`http` 按字面输入，长按选择 `https`，不会自动追加 `://`；`.com` 长按提供
`.net`、`.org`、`.edu`、`.gov`，以及输入语言对应的一个地区域名（例如 `.cn`、`.jp`、`.uk`）。
句号键同时显示主内容 `.` 和变体提示 `?`，长按输入 ASCII 问号。
地址快捷键始终输入半角字面文本，不随 Shift 或 IME 模式改为全角标点。
显式 `Letters` 仍按网址/邮箱作用域提供快捷键，因此从符号页返回时保留专用布局；
显式 `Numbers`、`Symbols`、`MoreSymbols` 优先于输入作用域。更换编辑器时恢复 `Automatic`。
切换输入模式保留当前符号页，不会自动跳回字母页。关闭由应用设置 `IsOpen=false`；
默认模板没有额外工具栏、输入法选择页或关闭按钮。

`ISoftKeyboardLayoutProvider.CreateLayout` 只需处理自己负责的上下文，不负责时返回 `null`。
`CompositeSoftKeyboardLayoutProvider` 按声明顺序查询，首个非 null 布局胜出，空布局也是有效结果。
建议依次注册特定 TSF profile、特定地区/语言、通用默认提供器。应用显式布局异常写入
`LayoutError`；扩展布局异常写入 `ExtensionError`，随后继续安全降级。
`SoftKeyboardLayoutRule` 将匹配条件与构建函数组合成一个小提供器，适合只扩展一个页面或一种输入法。

上下文包含只读 `Culture`、`InputScope`、字符串 `Page`、`ShiftState`、`InputMode`、`InputMethod`、
独立的 `InputModeButton`，以及一次性捕获的借用 `KeyboardLayoutHandle` 和独立于 Page 的
`SupplementalLayout`。
`InputMethod` 提供 ClassId、ProfileId、LanguageId 和可空的 `TouchLayoutPreference`，
可精确匹配输入法厂商与 profile。默认键帽按这个 HKL 解码，不应把显示文字当成输入法组合文本。
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
| DefaultSoftKeyboardLayoutProvider | 先选择完整语言组合，再回退到 Standard；不抢先分配数字或地址页 |
| Japanese / ChineseSoftKeyboardLayoutProvider | 各自拥有完整场景、页面与输入模式的路由，明确决定哪些布局复用通用实现 |
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
嵌套组合始终传递原始上下文，保留输入场景、页面、模式和 profile；不用伪装成普通文本上下文来复用其他提供器。

内置 URL/邮箱提供器目前只接管自己场景中的 Automatic/Letters 整页，明确将其他页面交还给所在语言组合。
这是这两个内置实现的策略，不是地址场景的接口限制。它们分别组装所有四行，只复用未定位的字母键与少量字面快捷键。
即使默认页面恰好共享四行形状，也可以独立修改 URL 的首行、邮箱的命令键或整个网格，不需要修改普通字母布局。

日语扩展的符号页包含 `「」`、`『』`、`・`、`※`、`〒` 等独立数据。这是扩展包的紧凑布局，
不声称逐键复刻 Win11 触摸键盘；附加假名面板独立实现，滑行输入和候选算法不由布局层实现。
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

基础库不解释输入法的转换模式。默认 Windows 来源优先从当前服务的 `GUID_LBI_INPUTMODE`
`ITfLangBarItemButton` 复制图标，并把 `OnClick` 作为切换行为。服务没有提供可用按钮时，
中文、日文和韩文输入法回退到 `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`：关闭显示 `A`，
开启分别显示 `中`、`日`、`한`，点击只切换 keyboard-open 状态。其他语言默认不显示回退按钮。
`InputModeButton` 公开实际使用的按钮呈现；基础库不读取 conversion compartment 决定其内容。

输入法模式不是统一的开启/关闭布尔值。只有应用或扩展明确设置 `InputModeAdapter`
（`ISoftKeyboardInputModeAdapter`）时，`InputMode` 才公开适配器定义的 `Id`、`IsNativeInput`
和 `CanSwitch`。解释模式不会改变按钮内容；默认仍采用基础库取得的按钮图标，点击时使用适配器行为。
确实需要接管呈现的特殊适配器可通过 `SoftKeyboardInputMode.Button` 显式提供完整按钮描述。
`SoftKeyboardTsfInputMode.GetState` 仍公开 open 和 conversion compartment 的原始整数，供已知服务的
适配器自行解释；核心只在上述二态回退中把 open 的零／非零显示为关闭／开启，始终不解释
conversion。读取异常保留最后一次有效观察，转换失败不会伪造新模式。

## 常驻头部与长按浮层

头部左侧的设置 `DropDownButton` 常驻并保留头部高度，右侧显示候选和分页按钮。
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
点击外部关闭；原键不会额外输入。浮层不参与键盘测量，关闭、隐藏、卸载、禁用和替换模板都会清理它。
Popup 使用不可聚焦按钮保留编辑器焦点，长按初始松手后才启用外部点击关闭，避免立即误关闭。
自定义键帽模板可以省略该可选部件，此时保留普通按键输入。变体按钮的 `DataContext` 是对应字符串。

键帽通过冒泡的 `InputRequested` 事件提交请求：`Key` 表示原按键，`AlternateText` 为 null
表示主动作，否则表示选中的变体字符串。动作实现 `ISoftKeyboardAlternateKeyAction` 时，变体由该动作处理；
其他变体继续走字面 Unicode 输入。`Invoke()` 和 UI Automation 也走同一请求入口。
键盘执行每次请求时仍校验目标、焦点和输入状态。键帽可单独使用，由宿主处理请求。

`InteractionStarted` 通知键盘取消其它键帽的交互；键盘关闭、切换布局、目标变化或窗口失活时，
通过 `CancelInteraction()` 取消活动键帽。键盘不操作键帽的计时器、捕获和浮层。
键帽依据 WPF 提升后的鼠标事件支持鼠标、笔和主触点，当前采用单活动手势，不提供多指组合键。
重复默认关闭，由布局模型显式启用；内置布局仅为退格和方向键启用重复。
变体优先于重复，重复之后松手不会再触发主动作。重复频率与初始延时采用系统键盘设置。
退格不附加软键盘的一次性或锁定 Shift，以便微软拼音在组合输入期间收到普通 Backspace；
退格也不消耗 Shift 状态，后续字母仍按当前大小写状态输入。

## IME 候选与输入法

输入模式转换键优先显示当前输入法在 `GUID_LBI_INPUTMODE` 语言栏按钮上提供的图标，点击时调用同一
`ITfLangBarItemButton.OnClick`。没有可用按钮时，已知支持二态开关的中、日、韩输入法通过 WPF 的
`ImeState` 读写 `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`，但不读取或改写 conversion compartment。
只读 `InputModeButton` 公开实际按钮呈现；
只读 `InputMode` 仅公开应用或扩展明确解释的状态。`ActiveInputMethod.IsActive` 只表示 profile 被选中。
符号键盘中的 `ABC` 键只负责返回字母键面，两者语义不同。

TIP 身份或 `ImmIsIME` 不能证明任意语言都具有二态输入法开关。基础库只为已知的中文、日文和韩文
启用 open/close 回退；未知语言保持隐藏。扩展适配器仍拥有最高优先级，可以解释已知输入法的状态，
但平台按钮的图标和点击行为不需要依赖状态解释。

TSF 的初始化和激活由 WPF 的 TextStore 管线负责。控件通过缓存且可降级的反射读取
`TextServicesHost.ThreadManager`；首个编辑器尚未初始化时，等待后续焦点或输入刷新重试。
候选桥接不设置 `TF_TMAE_UIELEMENTENABLEDONLY`：WPF 已经拥有并激活线程管理器，事后修改激活
标志会改变当前线程允许加载的输入法。系统触摸键盘使用 UI-less 激活模式；公开 TSF 允许宿主在
`BeginUIElement` 中决定是否隐藏输入法 UI，但 Windows Shell 对内置输入法还有未公开的产品策略。
控件只在键盘打开、候选属于 TSF 当前焦点文档且实现
`ITfCandidateListUIElementBehavior` 时继续协商。`Wpf.Zen.Ui.VirtualInput.Extensions` 的默认策略
明确内嵌已知 Windows 内置东亚输入法，第三方输入法保留原生浮窗。没有扩展明确决策时，核心仅对
实现 `ITfIntegratableCandidateListUIElement` 的候选使用内嵌；标准提交调用 `SetSelection` 与
`Finalize`。
若 TIP 在 sink 订阅前已打开候选，控件还会从同一个 `ITfUIElementMgr` 发现该候选，并仅在候选
存续期间短时同步其内容。微软拼音支持这组集成候选接口，可使用内嵌候选栏。
枚举器仅用于当前同步查询，并在当前 Dispatcher 上及时释放，缩短该快照的原生引用寿命。

输入法菜单及菜单项均不可获得键盘焦点；选择时编辑器持续持有其 TSF 文档和输入法状态。
候选接管统一检查接口能力、候选所属文档与当前焦点，不按输入法服务 GUID 强制回退。
获取或枚举后未被接管的 UIElement 引用在当前同步查询结束前释放，不留给后续 GC 清理。
`BeginUIElement` 协商显示，首次 `UpdateUIElement` 才读取候选内容。
模式按钮和自定义模式适配器完全由系统通知驱动，不使用定时轮询。切换 profile 时先为新 profile
建立 `open=off`、`conversion=0` 的状态基线，再按新 profile 选择适配器并发布模式、按钮和布局；
旧 profile 的 compartment 值不会带入新 profile。
未提供可控制候选接口的输入法继续使用自己的浮窗。

候选展示由接口能力、文档归属和扩展协商自动决定，不提供手动展示模式设置。

任何提交路径都以 TSF 后续通知决定候选显示，不因为 HRESULT 成功或按键入队就提前清空候选栏。

只有 `SoftKeyboard` 已打开且实际可见时才隐藏输入法的原生候选窗。关闭键盘，或折叠键盘及其
任意祖先（例如 Page、ContentDialog 的键盘区域）时，会先尝试恢复原生候选窗再释放内嵌候选；若
组合输入仍在继续，键盘重新显示后可重新发现仍存续且符合策略的 TSF 候选。
原生恢复依赖 TIP 正确实现 Show。自动策略会从元素开始时协商是否接管，而非等到隐藏后才尝试补救。

`GUID_TFCAT_TIPCAP_UIELEMENTENABLED` 只声明基础 UIElement 能力，第三方输入法即使注册该类别，
也不一定支持完整的集成候选交互。`ITfIntegratableCandidateListUIElement` 是 Windows 文档为
IME 集成控件定义的更强但可选的契约；实际 Microsoft Pinyin 也不一定暴露它。因此该接口只能
作为公开的保守回退信号，不能复刻 Windows Shell 的完整判断。默认扩展把已知 Windows 内置 TSF
服务身份作为显式产品策略，应用仍可用 `ISoftKeyboardTsfCandidateBehavior` 覆盖特定输入法。

输入法状态同时监听 WPF 输入语言与 `InputMethod.StateChanged`、TSF profile activation、
`GUID_LBI_INPUTMODE` 语言栏项目更新，以及 HWND 的 `WM_INPUTLANGCHANGE`、`WM_IME_NOTIFY`。
这些渠道只作为通知来源，不拼装跨通知的“完整快照”：profile 或输入语言通知刷新目录和活动
profile；open 通知只读取 open compartment；conversion 通知只读取 conversion compartment；
语言栏通知只刷新按钮呈现。WPF 状态事件读取触发该事件的 TSF compartment；
`WM_IME_NOTIFY` 则从发出消息的 HWND 所属 HIMC 读取 IMM 状态，不用一种通知去读取另一套状态源。
每次更新随后立即按当前已知字段重建受影响的布局。

profile 通知开始一代新状态。如果这一代尚未收到 open 或 conversion 通知，对应值分别按 off 和 0
解释，不读取也不保留上一代的值。因此从中文输入法切到微软日语输入法时，即使线程级 conversion
compartment 暂时仍保留中文值，软键盘也先显示直接输入 `A`，而不会误显示成“あ”。
`WM_IME_NOTIFY` 是 IMM input context 的状态通知。收到 `IMN_SETOPENSTATUS` 或
`IMN_SETCONVERSIONMODE` 时，控件借用该 HWND 的 HIMC，通过 `ImmGetOpenStatus` 或
`ImmGetConversionStatus` 读取相应真实值，读完立即释放。它不会转而读取线程级 TSF compartment，
也不会根据输入法品牌、按键来源或命令执行记录过滤通知。

模式按钮和 profile 菜单只发送原生命令，不根据命令返回值推测状态，也不在命令返回后主动读取。
只有来源真正发出变更通知时才读取并发布新状态。COM 通知可能同步嵌套在原生命令调用栈中；此时
控件就在通知回调中读取并应用对应字段，不等待命令返回，不把通知合并成无类型的失效标记，也不
通过 Dispatcher 延迟调度。

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

`InputMethodSource` 按控件创建会话，在所属 Dispatcher 上同步提供目录、激活和输入模式操作。
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

        public SoftKeyboardInputMode? GetInputMode(SoftKeyboardInputMethod inputMethod)
        {
            return new SoftKeyboardInputMode("latin", "A", "Latin input", false, true);
        }

        public bool TrySwitchInputMode(
            SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputMode currentMode)
        {
            return true;
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

`InputMethods` 是只读依赖属性，每次发布完整的 `IReadOnlyList<SoftKeyboardInputMethod>` 快照。
使用属性绑定观察替换；已持有的旧列表保持不变。替换来源时立即清空旧目录。

候选刷新、自定义异步动作和候选确认仍支持真正的异步等待。新刷新替代旧刷新，新动作替代旧动作；
候选提交和翻页共享一个输入操作，繁忙期间拒绝第二个请求。取消立即撤销旧操作的结果、错误和刷新
发布权限，执行者在任务结束后释放自己的资源。关闭、隐藏、禁用、换目标、卸载和失去前台输入所有权
会使相应操作失效。输入法会话在替换来源或卸载时释放；同步回调内发生替换时，在旧调用退出后释放。

候选元素结束与操作取消是不同事件：元素结束立即禁止继续向旧元素输入，但可以作为已发送操作的
完成确认。调用者取消则不能再报告成功。扩展、候选和输入法错误分别见 `ExtensionError`、
`CandidateError`、`InputMethodError`；被取消或过期操作的迟到结果与异常不会覆盖当前状态。

候选提交、翻页和输入法激活可由任意自定义模板通过 `SoftKeyboardCommands.CommitCandidate`、
`MoveToPreviousCandidatePage`、`MoveToNextCandidatePage`、`ActivateInputMethod` 连接，无需命名模板部件。
`SoftKeyboardCandidateList` 是可派生的 `ItemsControl`，使用标准 `ItemsSource`、`ItemsPanel`、
`ItemTemplate` 与 `ItemContainerStyle`；默认横向 `StackPanel` 可换成 `WrapPanel` 或自定义面板。

输入模式通过 WPF、TSF 和窗口消息通知即时同步；同语言 TIP 切换由 TSF profile activation 通知
覆盖，不依赖输入语言变化，也不使用周期复查。输入法菜单和菜单项不参与键盘焦点；没有有效编辑器持有焦点时仍
展示输入法列表，但禁用所有选择项。焦点离开原编辑器会使尚未执行的选择失效，不会重新聚焦
记忆中的目标。

候选展示与 TSF 组合文本是两个独立生命周期。控件通过 WPF 文本组合事件跟踪当前编辑器的预编辑
事务；切换键面、切换输入法、关闭键盘或插入符号和长按替代字符等直接 Unicode 文本前，
会先向仍保持焦点的输入法发送 Esc。这样由输入法按自身语义删除尚未确认的组合文本，而不是使用
可能保留拼音字母的 Enter，也不会用仅结束组合标记、但可能留下组合范围文字的强制终止方式。
控件仅在目标仍有键盘焦点且窗口位于前台时发送取消键，避免把 Esc 泄漏给其他编辑器或应用。
等待组合取消后执行的输入法操作属于发起时的输入会话。目标更换、关闭、隐藏、禁用、卸载或窗口
失活会使待执行操作失效；恢复原状态也不会复活旧操作。执行前还会核对原目标是否仍保持焦点。

## 系统键盘与兼容性

只有控件已加载、实际可见、`IsOpen=true`、宿主窗口在前台，而且有效编辑目标仍保持键盘焦点时，
控件才使用 Windows 10 1607+ 的窗口级 `IInputPaneInterop` / `IInputPane2.TryHide` 请求隐藏系统
输入面板。Windows 可以拒绝该协商请求；控件不会查找、移动或终止 `TabTip` 窗口。结果由
`SystemKeyboardSuppressionState` 公开。焦点变化时除即时请求外，还会在本轮 WPF 焦点处理完成后
合并补发一次隐藏请求，避免 `UIElement.Focus()` 随后调用 `TipTsfHelper.Show()` 覆盖隐藏结果。
有效抑制期间每 250ms 复核目标和前台窗口并请求隐藏，处理 Win11 异步弹出系统键盘的情况；
接口不可用时停止定时重试，待下一次焦点或显示状态变化再尝试。
整个过程不修改注册表、系统服务、全局触摸设置或 WPF 应用级开关，也不禁用编辑器 IME。
这属于自动隐藏而非 Windows 的强制禁止显示策略，系统拒绝请求时仍可能显示，异步弹出时也
可能短暂闪现；`Hidden` 表示隐藏请求被接受，不代表永久锁定系统键盘的可见性。

关闭或折叠控件、折叠其任意祖先、目标失效或焦点离开目标后，控件立即结束自己的抑制状态。
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

自定义键区使用 `SoftKeyboardKeyControl` 并绑定 `Key`，键盘不把普通 Button 的 `DataContext`
解释为按键。候选及页面按钮仍由键盘处理；应用按钮若未使用候选模型或动作 Tag，其 Click 事件继续向宿主冒泡。
长按在键盘禁用、隐藏、卸载、替换模板、窗口失活或鼠标捕获中断时结束；普通松手仍保留正确的单击抑制行为。

## 验证

运行定向自动测试（项目使用 xUnit v3 进程内运行器）：

```powershell
dotnet build tests/Wpf.Zen.Ui.UnitTests/Wpf.Zen.Ui.UnitTests.csproj
& tests/Wpf.Zen.Ui.UnitTests/bin/Debug/net10.0-windows/Wpf.Zen.Ui.UnitTests.exe -class '*SoftKeyboard*'
```

当前 VirtualInput 测试结果为 168 项、0 失败、6 项原生环境测试跳过、2 项显式测试未运行。
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

## 动态设置与附加布局

默认模板的 `PART_SettingsButton` 是 `Wpf.Zen.Ui.Controls.DropDownButton`，`Flyout` 绑定每个键盘独享的
`SettingsMenu`。菜单和菜单项保持不可聚焦，适合触摸／鼠标操作，避免切走编辑器的 TSF 文档。
自定义模板可直接使用 `SettingsItems` 数据建立自己的呈现方式，也可省略设置按钮，保留程序化 API。

默认菜单结构为：

- 键盘开关
  - 自动打开（`AnyFocus`）／手动打开（`Disabled`），独立单选组
  - 分隔线
  - 自动关闭（`FocusLeavesScope`）／手动关闭（`Manual`），独立单选组
- 有附加面板时才显示分隔线和“键盘布局”子菜单，使用 `RadioMenuItem`

原有 `TouchAndPen` 策略仍可通过属性设置；它不等同于菜单中的 `AnyFocus`，此时两个打开选项均不冒充选中。

设置引擎由以下部分组成：

| API | 用途 |
| --- | --- |
| `SettingsProvider` / `ISoftKeyboardSettingsProvider` | 从不可变状态快照生成菜单树，可组合默认策略或整体替换 |
| `SoftKeyboardSettingsContext` | 提供布局上下文、自动开关策略与当前 `AvailableLayouts` |
| `SoftKeyboardSetting` | 菜单数据；`Header`、`Icon`、`ItemStyle` 为呈现槽，`Children` 为子级，`RadioGroup`、`IsChecked` 为单选状态，`Apply` 为动作 |
| `SettingsItems` | 当前只读可观察菜单树，方便应用自定义呈现 |
| `RefreshSettings()` / `SettingsError` | 刷新外部策略数据；提供器或呈现数据出错时回退默认菜单并保留诊断 |
| `SelectSupplementalLayout(id)` / `SelectedSupplementalLayout` | 验证、选择并读取当前输入法的附加布局选择 |

附加布局通过 `ISoftKeyboardSupplementalLayoutProvider` 注册。每项把稳定 ID、显示名称、适用条件、
Automatic 首选规则和实际布局提供器放在同一不可变模型中，因此菜单、校验、记忆和生成不会分叉：

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

开关属性改变、输入法／模式／页面等相关状态改变和菜单打开时刷新设置；相同的原生轮询结果不会重复销毁菜单容器。
单选组按菜单实例隔离，多个键盘互不干扰。默认动作使用 `SetCurrentValue`，不会移除宿主对开关属性的绑定。
策略的生成方法不能更改键盘状态；自定义 `Apply` 动作作为普通应用回调执行，异常按普通回调传播。

```csharp
using Wpf.Zen.Ui.VirtualInput.Settings;

public sealed class AppKeyboardSettings : ISoftKeyboardSettingsProvider
{
    public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context)
    {
        foreach (var item in DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(context))
        {
            yield return item.Id == "keyboard"
                ? item with
                {
                    Header = "打开与关闭",
                    Icon = new Wpf.Zen.Ui.Controls.FluentIconSource(
                        Wpf.Zen.Ui.Controls.SymbolRegular.Settings24)
                }
                : item;
        }

        yield return new SoftKeyboardSetting("app.refresh", "刷新布局", k => k.RefreshLayout());
    }
}

// keyboard.SettingsProvider = new AppKeyboardSettings();
```

图标应优先提供 `IconSource` 等数据，默认菜单为每项创建独立的内容呈现器，并支持隐式 `DataTemplate`。
`ItemStyle` 应匹配该项实际生成的 `MenuItem`、`RadioMenuItem` 或 `Separator` 类型。

布局查询使用 `ITfFnGetPreferredTouchKeyboardLayout.GetLayout`，只获取布局类型及标识，不获取系统面板的几何和行为。
查询失败或输入法不实现接口时，`TouchLayoutPreference` 为 null；未知标识保留给自定义策略，不伪造系统支持。

日语扩展不提供独立假名面板。它解释标准的日语 open/conversion 状态，以便在直接输入、全／半角
字母、平假名和全／半角片假名之间正确判断是否使用原生输入，并为原生模式提供日语标点和符号布局。
模式按钮使用 `VK_DBE_ALPHANUMERIC` / `VK_KANJI` 作为输入法命令，不再把写入 conversion
compartment 当作切换成功；微软日语输入法可能回显写入值，却仍让实际按键走直接英文输入。专用模式键会让
输入法切换有效状态并自行发布新的 compartment 值。实际输入、组合与候选仍由当前日语输入法负责。

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

本机验证覆盖输入法枚举与切换、语言栏模式按钮、通用候选协商和真实 DropDownButton 菜单开关。
第三方输入法及其他 Windows/IME 版本仍需要各自验证。
