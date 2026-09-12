# SoftKeyboard

`SoftKeyboard` 是可参与 WPF 布局、保持编辑器焦点的软键盘控件，适合平板、Kiosk、POS
和需要让键盘稳定占据 Page、ContentDialog 或表单指定区域的应用。

软键盘位于独立的 `Wpf.Zen.Ui.VirtualInput` 程序集。XAML 使用专属 XML 命名空间，C# API
则按职责分布在 `Wpf.Zen.Ui.VirtualInput.Controls`、`.Layouts`、`.Layouts.Providers`、`.Input`
与 `.Settings`、`.Theming` 中；
通用 `Wpf.Zen.Ui` 程序集不再自动加载软键盘的类型和主题资源。

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

键盘及默认键帽均不可获得键盘焦点。每次输入前都会重新验证目标、作用域、前台 HWND、
启用/只读状态和焦点。普通键通过虚拟键进入 WPF 原有的命令、选区、撤销、死键和 TSF/IME
管线；普通符号和长按变体使用 Unicode `SendInput`，日语假名及其变体使用 IME 按键组合。
默认不包含 Emoji 目录、搜索或面板。

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
底行包含符号切换、输入状态、逗号、左右方向、空格、句号、输入法菜单和 Enter。
符号面提供 `Symbols` / `MoreSymbols` 两页，分别采用通用、中文和日文符号规则。
只有相应语言且 IME 开启时才使用中文或日文符号；韩文及未知语言不会套用中文标点。
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
切换输入状态保留当前符号页，不会自动跳回字母页。关闭由应用设置 `IsOpen=false`；
默认模板没有额外工具栏、输入法选择页或关闭按钮。

`ISoftKeyboardLayoutProvider.CreateLayout` 只需处理自己负责的上下文，不负责时返回 `null`。
`CompositeSoftKeyboardLayoutProvider` 按声明顺序查询，首个非 null 布局胜出，空布局也是有效结果。
建议依次注册特定 TSF profile、特定地区/语言、通用默认提供器。异常表示实现缺陷，不作为“不匹配”吞掉。
`SoftKeyboardLayoutRule` 将匹配条件与构建函数组合成一个小提供器，适合只扩展一个页面或一种输入法。

上下文包含只读 `Culture`、`InputScope`、字符串 `Page`、`ShiftState`、`ImeState`、`InputMethod`
及一次性捕获的借用 `KeyboardLayoutHandle`、独立于 Page 的 `SupplementalLayout`。
`InputMethod` 提供 ClassId、ProfileId、LanguageId 和可空的 `TouchLayoutPreference`，
可精确匹配输入法厂商与 profile。默认键帽按这个 HKL 解码，不应把显示文字当成输入法组合文本。
提供器不得在构建中更改焦点、输入语言或控件状态，也不要缓存编辑器；应用自己的数据变化后调用 `RefreshLayout()`。

`SoftKeyboardPages` 提供内置页面常量，页面集合不是封闭枚举。自定义页面使用如 `sample:kaomoji` 的
有命名空间的字符串，通过 `ShowPage()` 或 `SoftKeyboardKey.Navigate()` 导航。自定义页面同样遵守
组合取消和切换编辑器时恢复 `Automatic` 的规则；切换 Shift、输入状态或 profile 会重新解析当前页面。

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

日语符号页包含 `「」`、`『』`、`・`、`※`、`〒` 等独立数据。这是库的紧凑布局，
不声称逐键复刻 Win11 触摸键盘；附加假名面板独立实现，滑行输入和候选算法不由布局层实现。
需要不同网格、更多页面或不同功能时，直接注册独立提供器，无需修改公共符号网格或核心控件。

普通键优先使用内置 `VirtualKey`、`Text`、`ChangePage` 等语义。特有功能实现 `ISoftKeyboardKeyAction`，
通过 `SoftKeyboardKey.FromAction()` 放入布局；动作获得短生命周期的 `SoftKeyboardActionContext`，
可用 `TrySendKey()`、`TrySendText()`、`TrySendJapaneseKana()`、`ShowPage()` 和 `RefreshLayout()`。每次输入重新校验目标，
Unicode 输入仍经过组合取消；成功字符输入会消耗一次性 Shift。虚拟键的 Shift 参数由动作明确指定。
动作只能同步执行，上下文返回后失效；不得绕过此入口直接写编辑器或从提供器注入输入。
自定义动作本身的异常按普通应用回调传播，不作为布局错误处理。输入法状态读写仍由 `InputStateAdapter` 负责。

运行时 `SoftKeyboardLayout.Keys` 是不可变的扁平集合，每键显式指定 `Row`、`Column`、
`ColumnSpan`。所有行共享 `ColumnCount`，短行不会被拉伸；跳过的 Row 编号不预留空行。
默认使用 20 个半键列，普通键占 2 列。`SoftKeyboardPanel` 统一负责测量和排列，
`RowSpacing` / `ColumnSpacing` 默认均为 4 DIP。布局提供器应避免重叠并保持各状态的行数一致。
极小容器下间距和键位会缩小；触摸使用应为键盘提供足够空间。

对于没有遵循 WPF open/conversion compartment 约定的输入法，可设置
`InputStateAdapter`（`ISoftKeyboardInputStateAdapter`），实现状态读取和模式写入。
控件继续负责取消组合、恢复焦点、选择 profile 和定期复查；适配器只处理模式语义。
写入失败不会伪造开启状态，读取异常会显示未知状态，且不会再通过另一套 API 重复写入。

## 常驻头部与长按浮层

头部左侧的设置 `DropDownButton` 常驻并保留头部高度，右侧显示候选和分页按钮。
无候选、输入法关闭或原生候选回退时，候选区域隐藏，不会把整个头部折叠。

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

布局内标为 `A` / `中` / `日` / `한` 的键是输入模式开关：输入法打开时点击会切到直接字母输入，再次点击会恢复
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
`ITfCandidateListUIElementBehavior` 且符合 `CandidateMode` 策略时接管候选；标准提交调用 `SetSelection` 与 `Finalize`。
若 TIP 在 sink 订阅前已打开候选，控件还会从同一个 `ITfUIElementMgr` 发现该候选，并仅在候选
存续期间短时同步其内容。微软拼音支持这组标准 UIElement 接口，可使用内嵌候选栏。

`CandidateMode` 默认 `Automatic`，设置菜单的“候选显示”可选择自动、输入法浮窗（`Native`）、
键盘内嵌尝试（`Embedded`）。策略属于当前控件实例，不会修改输入法配置；已接管的候选元素保持其
呈现方式直到结束，避免切换设置时丢弃正在输入的内容。完成或取消当前组合后，新策略完整生效。

自动策略通过固定候选 GUID 识别小狼毫并保留其原生浮窗。小狼毫关闭 `inline_preedit` 时，拼音预编辑
保存在它自己的浮窗中，WPF 文档可能只得到占位空格。它的候选接口不提供该字符串，也不实现
`ITfReadingInformationUIElement`；隐藏浮窗会同时隐藏唯一的拼音预览。WPF `TextStore.OnUpdateComposition`
从文档组合范围读取文字，反射同一范围不能恢复输入法没有写入的内容。
源码依据：[小狼毫候选实现](https://github.com/rime/weasel/blob/master/WeaselTSF/CandidateList.cpp)、
[组合文本实现](https://github.com/rime/weasel/blob/master/WeaselTSF/Composition.cpp)。

需要内嵌小狼毫时，用户可自行启用输入法的 `style/inline_preedit: true`，重新部署配置后选择 `Embedded`。
配置说明见[小狼毫定制文档](https://github.com/rime/weasel/wiki/Weasel-%E5%AE%9A%E5%88%B6%E5%8C%96)。
控件不会改写用户的 Rime 配置。此显式模式下，小狼毫的标准行为接口只改变 UI 高亮／关闭 UI，
没有相应的引擎提交操作；其集成接口的 `OnKeyDown` 同样没有实现选词。
因此对当前页 1–9 项候选，点击第 N 项只发送一次数字键 N，由输入法引擎直接完成选择，
不经过逐项高亮移动，也不追加 Space。此适配依赖 Rime 的标准数字选词绑定，
不会把其他输入法的全局候选索引直接当作页内数字，也不会为超过 9 项的页面猜测快捷键。
数字键发送前核验编辑器和候选所有权，发送后保留候选快照以阻止重复点击；
新候选列表或 UI 元素结束后允许下一次选择，支持部分转换后的连续选词。
750ms 未观察到候选变化时报告诊断，但不重发数字或补空格，以免重复提交。
自定义 Rime 方案若重绑定数字键，需要额外适配；
失败原因可从 `CandidateCommitError` 读取。该适配不访问小狼毫内部对象或进程通信协议。
任何提交路径都以 TSF 后续通知决定候选显示，不因为 HRESULT 成功或按键入队就提前清空候选栏。

只有 `SoftKeyboard` 已打开且实际可见时才隐藏输入法的原生候选窗。关闭键盘，或折叠键盘及其
任意祖先（例如 Page、ContentDialog 的键盘区域）时，会先尝试恢复原生候选窗再释放内嵌候选；若
组合输入仍在继续，键盘重新显示后可重新发现仍存续且符合策略的 TSF 候选。
原生恢复依赖 TIP 正确实现 Show；小狼毫仅在 BeginUIElement 允许显示时创建原生窗口，
因此自动策略从元素开始时就不接管它，而非等到隐藏后才尝试补救。

公开 TSF 的 UIElement 能力类别与 QueryInterface 能用于协商接口，但不保证其实现完整。
`ITfIntegratableCandidateListUIElement` 也不是“完美兼容”的认证。没有找到系统触摸键盘完整判定流程的
公开契约；本库采用明确的已知兼容性规则，不声称复刻系统内部策略。

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

输入状态通过 WPF 的 StateChanged、焦点和输入语言事件即时同步；控件拥有前台编辑焦点时，
还每秒复查配置和状态，覆盖同语言输入法切换没有语言变更事件的情况。复查只读取系统状态，
不会主动切换用户输入法。输入法菜单打开期间暂时保留目标作用域，选择前先恢复编辑器焦点。

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

自动测试覆盖布局模型、四行布局边界、符号页面、日文变体、系统状态回读、
适配器失败、固定头部、菜单生命周期、浮层定位与单次提交、候选分页、组合取消以及目标隔离。
`GeneratedLetterKey_InvokeInsertsThroughWindowsInputStream` 和
`InstalledMicrosoftPinyin_PublishesCandidatesToEmbeddedSurfaceWhenAvailable` 是显式集成测试，
需要可激活前台窗口的交互桌面；后者还需要启用微软拼音。可在满足条件的桌面使用
`-explicit on -method '*测试方法名'` 单独运行，避免无人值守测试向用户桌面注入按键。

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
现代微软注音在部分桌面环境不导出这项旧接口，因此内置策略另有精确的 ClassId + ProfileId 回退，
不会仅凭 `zh-TW` 或显示名称把仓颉、大易等输入法识别为注音。

- 日语默认维持标准键盘，设置中可选“50音”或“12键”。50音包含五十音网格与功能行；12键为四行三列假名组，加一行功能键。
- 12键短按输入主音，长按选择同组其他音；同时提供小字、浊音和半浊音。复用现有长按浮层，不实现 Win11 专有的 flick／滑行手势。
- 日语附加面板输入时明确使用全角平假名／假名按键模式，IME 的此模式在输入后继续保留。
  按键由 Windows 当前 HKL 的假名转换表反查，支持 101／106 键配置差异，组合与候选仍由 IME 负责。
- 识别为标准台湾注音时，默认选择 41 个注音／声调键，加一行功能键；IME 关闭后显示字母键盘，重新开启恢复注音。
- 注音功能行的 `↓` 是普通方向键，只按输入法当时的状态请求选字／导航，不能保证出现内嵌候选。
  控件只能呈现输入法实际通过 TSF 发布的候选，不能凭空生成候选或靠补方向键模拟系统触摸键盘的候选行为。
  [微软注音说明](https://support.microsoft.com/zh-tw/windows/hardware/input-devices/microsoft-traditional-chinese-ime)
  描述输入法自己的窗口和按键语义，不证明自定义软键盘能接管候选。
  本机测试限定 Profile `B2F9C502-1742-11D4-9790-0080C882687E`，输入 `ㄋㄧˇ` 后再按 Down；
  单独输入 `ㄋ`，以及此时按 Down，均复现没有候选的现象；此阶段与已转换汉字的选字列表不同。
  额外检查公开查询渠道：本机微软注音的 `GetFunctionProvider` 返回 `0x80040503`，
  TSF 系统提供器对 `ITfFnSearchCandidateProvider` 和 `ITfFnGetLinguisticAlternates` 均返回 `E_NOINTERFACE`。
  因而当前实现尚不能提供系统触摸键盘那样的未完成音节实时候选，问题未解决。
  这些结果限于本机版本，也不足以确认系统触摸键盘具体使用哪个内部接口。
- 附加选择与数字／符号 `Page` 分开，临时进入符号页再返回时仍保留所选面板；网址、邮箱、自动数字作用域保留专用布局。
- 布局选择按 `(LanguageId, ClassId, ProfileId)` 保存在当前控件实例内，切换输入法后再切回来可恢复。
  控件不自动写入配置文件或注册表；需要跨启动持久化时由应用存储，并在对应输入法激活后调用选择 API。

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

本机验证覆盖小狼毫非首候选提交、微软日语两种面板的组合与浊音／半浊音／小字输入、微软注音、
真实 DropDownButton 菜单开关，以及切换输入法后恢复布局选择。第三方自定义方案及其他 Windows/IME 版本仍需要各自验证。
