# TSF 输入模式状态观察笔记

## 背景

软键盘需要显示当前输入法的输入模式，并在可能时提供模式切换按钮。实践证明，
不能仅凭 TSF Profile 激活通知以及
`GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`、
`GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` 两个 compartment，构造一套适用于所有
输入法的统一解释规则。

本文记录目前从微软拼音、微软注音、讯飞等输入法的实际行为中得到的结论。这里记录的
数值含义均属于对应 TIP 的已观察行为，不应推广为 TSF 的统一契约。

## 已观察的输入模式数值

下表中的“开”和“关”保留各输入法观察时的原始模式命名；`open` 与 `conversion` 均为
对应 TSF compartment 的原始整数值。除表中明确说明的情况外，不应从其他输入法遗留的
数值推导当前输入法状态。

| 输入法 | ClassId | 关闭状态 | 开启状态 | 备注 |
| --- | --- | --- | --- | --- |
| 微软拼音 | `{81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E}` | `open = 1`，`conversion = 0` | `open = 1`，`conversion = 1025 (0x0401)` | conversion 区分开关。 |
| 讯飞输入法 | `{B722B5D7-0C4C-4933-A7B9-DF8C91F2C643}` | `open = 1`，`conversion = 0` | `open = 2`，`conversion = 0` | conversion 未观察到参与模式区分。 |
| 微信输入法 | `{86598FB9-66A2-463E-B9C2-AEB906D477AD}` | `open = 0`，`conversion = 0` | `open = 1`，`conversion = 0` | conversion 未观察到参与模式区分。 |
| 微软注音 | `{B115690A-EA02-48D5-A231-E3578D2FDF80}` | `open = 1`，`conversion = 0` | `open = 1`，`conversion = 1` | conversion 区分开关。 |
| 微软日语 IME | `{03B5835F-F03C-411B-9CE2-AA23E1171E36}` | `open = 0`，conversion 保留 | `open = 1`，conversion 随具体输入模式变化 | 已观察到的 conversion：`9 (0x09)` 平假名、`11 (0x0B)` 全角片假名、`8 (0x08)` 全角字母数字、`3 (0x03)` 半角片假名。 |

微软日语 IME 的关闭状态不会可靠地清空 conversion；读取到的 conversion 可能仍是该输入法
此前模式的值。因此，解释日语 IME 的关闭状态时应以 `open = 0` 为准，不应将保留的
conversion 误判为当前开启模式。

## Profile 激活不是状态稳定边界

收到活动 Profile 的激活通知，只能说明该 TIP 已被选为活动 Profile。它不保证该 TIP 已经：

- 完成输入模式状态初始化；
- 写入所有相关 compartment；
- 为当前焦点文档完成上下文切换；
- 注册或更新 `GUID_LBI_INPUTMODE` 语言栏项目；
- 发布完与本次切换相关的状态通知。

因此，在 Profile 激活回调中立即读取 open 和 conversion，不能认为得到了该输入法的最终
状态。读取结果可能是切换过程中的瞬时状态，也可能由新旧输入法分别留下的值组成。

更重要的是，open 与 conversion 是两个独立状态，不存在可供外部读取者使用的原子快照。
即使两者都能成功读取，也不能据此认定它们属于同一个输入法状态转换。

`GUID_LBI_INPUTMODE` 同样不构成 Profile 激活时的稳定状态。TIP 可能已经报告 Profile
激活，但尚未加入语言栏项目，或者此时返回的项目还不能查询为
`ITfLangBarItemButton`。因此，Profile 激活后立即查询语言栏按钮，与立即读取 open 和
conversion 一样，都不能作为最终状态发布依据。

## 输入法不一定同时使用 open 和 conversion

TIP 可以选择只维护它实际需要的状态。不能假设每次 Profile 切换都会收到 open 和
conversion 两种通知，也不能假设未收到通知的 compartment 会被重置。

以讯飞输入法的当前观察结果为例：

- 讯飞只关心 `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`；
- `openValue = 1` 表示关闭中文输入；
- `openValue = 2` 表示开启中文输入；
- 它不参与 conversion 状态；
- 切换到讯飞时，它可能只发布 open 的变化通知；
- conversion 中仍可保留上一个输入法写入的值。

因此，讯飞的 open 与遗留 conversion 拼在一起，并不构成讯飞的有效复合状态。讯飞适配器
应只解释讯飞实际拥有的 open 值，不应要求 conversion 同时满足某个条件。

这也说明 `OPEN/CLOSE` 的整数不能被通用地当成布尔值。每个 TIP 对原始数值的使用方式都
必须以实际验证结果为准。

## 焦点变化为何可能使显示恢复

第三方 TIP 可能在获得 TSF 文档焦点后才完成内部上下文初始化、语言栏项目注册或状态
发布。焦点切出再切回可能促使 TIP：

- 处理 TSF 文档焦点变化；
- 注册或更新 `GUID_LBI_INPUTMODE`；
- 发布语言栏图标或状态变化通知；
- 在新的文档上下文中重新应用自身状态。

这不等于 open 和 conversion 必然被重新读取或纠正。必须通过日志区分以下两类情况：

1. 收到了 compartment 通知或执行了新的完整状态读取；
2. 只收到语言栏项目更新，因原生图标此时可用而恢复显示。

讯飞问题的现有日志属于第二种情况：焦点回来后，`GUID_LBI_INPUTMODE` 查询成功，图标因此
出现；日志并未证明 conversion 被讯飞更新。

## 语言栏通知的边界

语言栏存在两类容易混淆的通知：

- `ITfLangBarItemSink.OnUpdate` 表示已知语言栏项目的图标、文字、提示或状态发生变化；
- `ITfLangBarEventSink.OnThreadItemChange` 表示某个线程的语言栏项目集合发生变化。

`OnUpdate` 不是“语言栏按钮安装完成”通知。项目提供者在图标或状态变化后主动调用它，
但 TSF 不保证项目加入后一定补发一次包含全部属性的更新。`OnThreadItemChange` 更适合用于
发现项目加入或移除，但它不提供具体项目身份，也不保证回调时项目的所有属性已经稳定。

讯飞问题的现有日志表明：

- Profile 激活时没有收到 `TF_LBI_ICON | TF_LBI_STATUS`；
- 此时查询 `GUID_LBI_INPUTMODE` 失败；
- 焦点切出再切回后才收到 `TF_LBI_ICON | TF_LBI_STATUS`；
- 收到该更新后，`ITfLangBarItemButton` 查询成功。

这进一步证明，Profile 激活、compartment 状态完成和语言栏项目就绪是相互独立的事件，
不能用其中任意一个事件代表整个输入模式状态已经完成初始化。

### 语言栏按钮不是声明式能力

TSF 没有提供一个 capability category，用来声明“当前 TIP 提供 `ITfLangBarItemButton`”。
`ITfLangBarItemButton` 是具体语言栏项目对象支持的接口，而不是 TIP 级别的静态能力。

因此，语言栏按钮只能运行时探测：

1. 通过 `ITfLangBarItemMgr` 枚举或按 GUID 获取当前语言栏项目；
2. 对取得的 `ITfLangBarItem` 查询 `ITfLangBarItemButton`；
3. 若目标是输入模式按钮，还必须继续确认项目 GUID，例如 `GUID_LBI_INPUTMODE`。

这类探测结果应视为 Runtime Capability，而不是 TIP 的声明式 Capability。项目存在、
`QueryInterface` 成功，只能证明当前时刻存在一个可调用的语言栏按钮，不能证明它的图标、
文字或命令已经与 TIP 的真实输入状态同步。

### `GUID_LBI_INPUTMODE` 不能作为状态真值

后续实测发现，即使 `GUID_LBI_INPUTMODE` 可以成功取得，而且可以读取图标并调用命令，
其呈现仍可能与系统托盘显示不同步，甚至出现相反状态：

```text
系统托盘：英    软键盘读取：中
系统托盘：中    软键盘读取：英
```

这说明 `GUID_LBI_INPUTMODE` 存在、可以查询为 `ITfLangBarItemButton`、`GetIcon` 成功，均不能
推出该项目当前呈现就是 TIP 的可靠状态快照。语言栏项目和系统托盘最终显示之间可能仍有
内部缓存、异步更新或系统私有状态通道。

因此，`GUID_LBI_INPUTMODE` 只能作为某个 TIP 的候选能力入口。只有在针对该 TIP 的实机验证中
确认其读取、更新通知、命令行为和实际输入行为长期一致后，才能由适配器采用。

附注：这可能与获取时机有关，实测中还发现：延迟一段时间获取图标则能与系统托盘图标保持一致。

## IMM 窗口消息实验

为避免软键盘现有 TSF 观察逻辑干扰结果，在图库程序的 Sandbox 窗口中加入了独立实验。
实验被测窗口直接记录 `WM_INPUTLANGCHANGE`、`WM_IME_NOTIFY` 等窗口消息，并在每条关键
消息到达时，通过该窗口当前的 HIMC 调用 `ImmGetOpenStatus` 和
`ImmGetConversionStatus`。实验还在消息之后继续采样至 500 毫秒，用来判断状态是否延迟
变化。

### 中文首次切换到微软日语

本轮切换的关键时序如下：

```text
02:04:42.509  HKL 已为 ja-JP，open=0，conversion=25
02:04:42.513  WPF InputLanguageChanged(ja-JP)
02:04:42.515  WM_INPUTLANGCHANGE，open=0，conversion=25
02:04:42.520  IMN_SETOPENSTATUS，open=0，conversion=25
02:04:42.522  IMN_SETCONVERSIONMODE，open=0，conversion=25
```

随后直至 500 毫秒的采样始终为 `open = 0`、`conversion = 25 (0x19)`。`0x19` 可分解为
`NATIVE | FULLSHAPE | ROMAN`。这组值在 HKL 已经切换为 `ja-JP` 后读取，并由日语输入法的
两条状态通知确认，因此本轮 `conversion = 25` 不是中文输入法遗留值，而是微软日语输入法
关闭时保存的模式值。

### 日语输入法内部切换模式

在日语输入法内开启输入模式时，观察到两条相邻通知：

```text
02:04:48.845  IMN_SETOPENSTATUS，open=1，conversion=25
02:04:48.848  IMN_SETCONVERSIONMODE，open=1，conversion=9
```

这说明一次用户操作可以分两步更新 HIMC。第一条通知到达时，open 已经更新，而 conversion
仍是本次转换前的值；约 3 毫秒后的第二条通知才把 conversion 更新为
`9 (NATIVE | FULLSHAPE)`。因此，不能把第一条 `IMN_SETOPENSTATUS` 到达时读取的两个字段
直接当作完整的最终快照。可以把同一消息队列批次内连续到达的状态通知合并，在该批消息
处理完后读取并发布一次。

### 在中文和日语之间往返

本次实验随后观察到以下稳定状态：

```text
日语首次激活：             open=0，conversion=25
日语内部切换后：           open=1，conversion=9
切换回中文：               open=2，conversion=1
再次切换到日语：           open=1，conversion=9
```

再次激活日语时，它恢复了自己上次的 `open = 1`、`conversion = 9`；中文则恢复为
`open = 2`、`conversion = 1`。整个过程中 HIMC 句柄始终是同一个 `0xF1095D`。这表明同一个
窗口 HIMC 可以在输入法切换时装载各输入法分别保存的状态；HIMC 句柄相同，不表示其中的
状态一定属于上一输入法。

两次输入法切换都收到以下消息：

- `WM_INPUTLANGCHANGE`；
- `WM_IME_NOTIFY / IMN_SETOPENSTATUS`；
- `WM_IME_NOTIFY / IMN_SETCONVERSIONMODE`。

在本次实验中，`WM_INPUTLANGCHANGE` 到达时已经可以读到新输入法的正确状态，后续状态通知
又明确标识了 open 和 conversion 的更新。10 至 500 毫秒的延迟采样没有观察到进一步变化，
因此这些切换不需要固定等待 100、250 或 500 毫秒。

日志中的 `after-dispatch` 有时延迟几十毫秒，这是实验代码通过 WPF Dispatcher 排队后才
执行采样造成的，不能解释为输入法状态经过同样长的时间才稳定。判断真实时序时，应以窗口
钩子直接记录的消息行及其中的即时读数为准。

### 某些 TIP 不向 IMM 发布内部输入状态

后续对其他输入法的多次测试表明，IMM 窗口消息也不是通用状态来源。部分 TIP 在内部切换
输入状态后：

- `ImmGetOpenStatus` 返回值不变；
- `ImmGetConversionStatus` 返回值不变；
- 不产生对应的 `WM_IME_NOTIFY`；
- 甚至整个模式切换过程没有可用于识别状态变化的 Win32 窗口消息。

小狼毫输入法已经观察到这类行为：实际输入模式已经发生变化，但 Win32 API 读取到的 open
和 conversion 始终不变。由此可以确认，某些 TIP 的模式状态只存在于自身内部上下文，既不
要求同步到标准 TSF compartment，也不要求同步到 IMM 兼容状态。

因此，IMM 路径与 TSF compartment 路径具有相同的根本边界：主动读取只能读取 TIP 已经公开
到该通道的数据，等待更长时间不能使一个从未发布的内部状态自动出现。

附注：某些输入法除了输入法内部调整状态后收不到 `WM_IME_NOTIFY` 消息，甚至该输入法激活
后也收不到任何消息，进一步证明 IMM 窗口消息也不可靠。

### 实验结论与边界

本次结果修正了仅依据 TSF compartment 日志形成的悲观判断：

- TSF Profile 激活回调仍然不是可靠的状态稳定边界；
- 目标窗口的 IMM 消息在本次微软日语和中文输入法实验中提供了及时、明确的状态通知；
- IMM 状态与窗口 HIMC 关联，和 TSF 线程 compartment 是不同的观察路径；
- 状态可以在几毫秒内分多条消息更新，发布方需要合并当前消息批次，而非设置较长静默期；
- `ImmGetOpenStatus` 的非零值不能在采集层过早压缩成布尔值，讯飞实测的 `1` 和 `2` 具有
  不同含义，必须保留原始整数；
- 当前证据来自图库 Sandbox 的被测窗口，仍需验证软键盘所服务的真实目标窗口能否收到同样
  的消息，以及跨进程目标窗口是否需要在目标线程内建立观察机制。

因此，IMM 窗口消息可以作为部分 TIP 的附加观察源，但不能提升为通用模式状态来源。TSF
可以继续负责确认活动 Profile 身份；TSF compartment、IMM 状态和语言栏项目都只能作为
由对应 TIP 适配器选择使用的候选状态源。

## 架构结论

### 可以通用化的部分

基础设施可以统一负责采集原始信号：

- 活动 Profile、ClassId 和 ProfileId；
- 目标窗口的 HKL、HIMC、`WM_INPUTLANGCHANGE` 和 `WM_IME_NOTIFY`；
- 通过 IMM 读取的原始 open、conversion 和 sentence 值；
- open compartment 的原始值及其通知；
- conversion compartment 的原始值及其通知；
- `GUID_LBI_INPUTMODE` 语言栏项目及其更新；
- WPF 焦点和 TSF 文档焦点变化；
- 每次观察所属的 Profile、焦点文档和状态代次。

基础设施只应陈述“观察到了什么”，不应赋予原始整数跨输入法的通用含义。

### 声明式能力与运行时能力

后续探测应先区分 TIP 声明的能力和运行时实际暴露的对象。

对于标准 capability category，可以先查询 TIP 是否声明对应能力。例如：

- `GUID_TFCAT_TIPCAP_INPUTMODECOMPARTMENT` 表示 TIP 声明支持 input mode compartment；
- 只有声明该能力，才值得继续读取相应 input-mode compartment；
- 即使声明成功，仍需由适配器验证数值含义和通知行为。

语言栏能力则不能通过类似 capability flag 判断。是否存在 `ITfLangBarItemButton`、是否存在
`GUID_LBI_INPUTMODE`，应在运行时直接枚举或查询语言栏项目后探测。

因此，可以把能力区分为：

```text
DeclaredCapabilities
    InputModeCompartment
    ...

RuntimeCapabilities
    HasLangBarItemButton
    HasInputModeLangBarItem
    ...
```

两类能力都只能说明“这个入口值得尝试”，不能自动提升为“这个入口的状态可靠”。

附注：语言栏按钮至今仍不稳定，决定当前版本暂时取消这一路径。

### 无法通用化的部分

以下行为必须按 TIP 身份适配：

- 哪些 compartment 对该 TIP 有效；
- 每个原始整数或位字段的含义；
- 多个状态是否需要组合解释；
- 输入模式切换应调用语言栏命令，还是写入某个 compartment；
- 在原生语言栏项目不可用时，如何生成可靠的替代显示。

因此，准确的模式识别和切换没有覆盖所有输入法的通用实现。正确方向是建立通用观察层，
再按 ClassId/ProfileId 选择经过验证的 TIP 适配器。未知 TIP 不应套用其他输入法的规则。

## Profile 激活后的处理原则

Profile 激活时立即读取的 open/conversion 只能作为诊断或候选观察，不能直接被定义为稳定
状态。后续设计应遵循以下原则：

1. Profile 激活开启新的观察代次，并使上一 Profile 的解释结果失效。
2. 不要求新 TIP 必须发布 open 和 conversion 两类通知。
3. 每种通知只证明对应状态源发生了变化，不证明其他状态源已经同步完成。
4. 只有适配器声明参与的状态源，才能用于解释该 TIP。
5. 焦点文档变化、相关 compartment 通知和语言栏项目更新都可以触发重新观察。
6. 延迟读取可以帮助等待 TIP 收敛，但不能创造 TIP 未提供的状态语义。
7. 所有异步或延迟结果必须校验 Profile 和焦点代次，避免旧结果覆盖新状态。
8. Profile 激活通知适合作为重新探测入口；激活后延迟读取只能帮助等待已公开状态收敛，
   不能创造 TIP 从未向 TSF、IMM 或语言栏发布的内部状态。
9. 未经验证的 TIP 不应因为存在 compartment、IMM 状态或 `GUID_LBI_INPUTMODE` 就自动获得
   模式切换能力；无法可靠解释时，应保持未知或不提供切换，而不是猜测。

## 状态收敛与原子发布方案

以下方案是早期针对 TSF compartment 观察路径形成的设计。后续实验已经证明，收敛机制只能
处理“状态已经公开但尚未稳定”的时序问题，不能解决 TIP 根本不向标准通道公开内部模式的
问题。因此，本节保留为已验证 TIP 的实现参考，不再作为未知 TIP 的通用策略。

输入模式观察应从“每条通知立即刷新界面”改为“收集一个一致快照后原子发布”。针对 TSF
路径曾建议采用以下状态过程：

```text
Stable
  │ Profile 激活
  ▼
Stabilizing
  │ 收集通知并更新草稿快照，不发布界面
  ▼
Committed
    由扩展解释快照，并原子刷新布局、模式和按钮
```

### 开始收敛

收到活动 Profile 通知后应执行以下操作：

1. 增加观察代次，并记录新的 ClassId、ProfileId 和焦点文档身份。
2. 创建仅供内部使用的草稿快照。
3. 不在激活回调中立即读取 open、conversion 或语言栏按钮作为发布状态。
4. 使上一 Profile 的解释结果失效。
5. 隐藏模式切换按钮，或者显示不包含旧输入法含义的禁用占位。
6. 启动本次 Profile 的有限状态收敛窗口。

不应继续显示上一输入法的图标或文字，即使按钮处于禁用状态。旧呈现会让用户误以为新
输入法已经处于该模式。键盘其余部分可以短暂保持上一稳定布局，但不能接受依赖新输入法
状态的模式切换操作。
批注：键盘应该在收到活动 Profile 通知后立即刷新到基本布局，如果保留“上一稳定布局”，则可能产生输入法无法解释、转换输入键的问题。

### 收集草稿状态

收敛期间，各类通知只更新自己拥有的草稿字段：

- open 通知更新 `OpenValue`；
- conversion 通知更新 `ConversionValue`；
- `ICON/STATUS` 通知标记语言栏呈现需要重新读取；
- 语言栏项目集合变化触发 `GUID_LBI_INPUTMODE` 可用性复查；
- 焦点变化更新焦点文档代次，并触发新的观察机会。

这些变化只写入草稿，不立即更新 `InputMode`、`InputModeButton`、切换路由或键盘布局。
草稿字段还应记录观察时间和来源，例如 `Notification` 或 `FinalRead`，便于诊断状态是由
主动读取还是原生通知产生。

compartment 通知本身不携带 Profile 身份。新 Profile 激活后收到的通知，理论上仍可能是
上一 TIP 的收尾操作。观察代次可以阻止旧的异步任务覆盖新结果，但不能为原生通知补充
不存在的所有者信息。收敛窗口和 TIP 专用解释只能降低这种不确定性，不能彻底消除它。

### 判定收敛

收敛不应只使用一次固定延迟。更合理的方式是同时设置：

- 静默窗口：最后一次相关通知后等待一小段时间；
- 最长截止时间：防止没有完整通知的 TIP 使界面永久等待。

初始参数可以考虑约 100 毫秒静默窗口和约 500 毫秒最长截止时间，最终数值应根据实机
日志调整。收到相关通知时更新静默窗口，但不能突破最长截止时间。

收敛结束时执行一次完整读取，而不是采用 Profile 激活瞬间的读取结果。提交前必须确认：

- 活动 Profile 仍与本次观察一致；
- 观察代次没有改变；
- 焦点文档仍然匹配；
- 没有更新的相关通知等待处理。

校验失败时丢弃本次草稿，由新的观察代次负责后续状态。

### 原子提交

通过校验后，将同一份原始快照交给对应扩展解释，再一次性发布：

- `ActiveInputMethod`；
- `InputMode`；
- `InputModeButton`；
- 输入模式切换路由；
- 依赖输入法和模式的键盘布局。

扩展继续拥有解释权，并明确声明自己消费哪些状态源。讯飞可以只解释 open，微软拼音或
微软注音可以解释各自验证过的状态组合。基础设施不得要求所有扩展同时消费 open、
conversion 和语言栏按钮。

延迟与收敛机制只能帮助等待 TIP 完成初始化，不能创造 TIP 从未提供的状态。即使存在有效
语言栏按钮，也不能仅凭查询成功认定其状态可信。只有已进入兼容性白名单的 TIP 才允许由
适配器启用模式切换；未知 TIP 默认保持无模式切换能力。

## 当前版本的最终收敛决策

经过 TSF compartment、语言栏、IMM 窗口消息等多条路径的实测，当前版本不再尝试提供覆盖
所有输入法的通用模式切换键。

基础库默认行为调整为：

- 未知 TIP 不显示输入法模式切换键；
- 不根据语言、HKL、open/conversion 或 `GUID_LBI_INPUTMODE` 的存在自动推断支持；
- 不再通过 `GUID_LBI_INPUTMODE` 方案提供模仿输入法模式切换按钮，只保留 open/conversion 路径。
- 仅对经过实机验证的 TIP 开放模式识别和切换能力；
- 适配器至少按 ClassId/ProfileId 匹配，必要时还应限制输入法版本。

一个 TIP 进入兼容性白名单前，至少需要验证：

1. Profile 激活后能够可靠确认 TIP 身份；
2. 当前模式能够稳定读取；
3. 模式变化能够可靠检测，或者能够通过可靠的主动读取重新确认；
4. 快速连续切换不会出现状态混乱、滞后或回滚；
5. 焦点切换、文档上下文变化和 Profile 重激活后仍然保持一致。

这里采用白名单而不是黑名单，是因为目前已经确认的失败模式不仅包括“接口不存在”，还包括
“接口存在但返回的状态不可信”。后者无法通过简单的 capability 检测排除。

## 长期方向：自有完整 TSF TIP

长期计划是实现自有输入法，并完整实现软键盘依赖的 TSF 和 Windows 兼容能力。目标不是再从
多个外部状态源猜测当前模式，而是由输入法内部维护唯一的 `InputModeState`，再把同一个状态
同步投影到：

- TSF input-mode compartment；
- TSF Profile、焦点和相关状态通知；
- 必要的 IMM/Windows 兼容状态与消息；
- 软键盘专用的模式查询和切换入口。

这样可以保证“内部实际输入行为”与 TSF、语言栏、Windows 兼容层以及软键盘显示来自同一个
状态源。计划中的完整 TSF 注音输入法还可以作为 `VirtualInput` 的 reference implementation，
用于验证这些协议在正确实现时应当如何协同，并作为判断第三方 TIP 兼容性问题的基准。

## 日志要求

为了继续验证不同 TIP 的行为，日志应明确区分：

- `PROFILE`：Profile 激活或停用；
- `OPEN/CLOSE`：open compartment 的原始值；
- `CONVERSION`：conversion compartment 的原始值；
- `LANGUAGE BAR`：语言栏项目注册、状态或图标更新；
- `FOCUS`：WPF 焦点与 TSF 文档焦点变化；
- `SNAPSHOT`：主动读取，以及该读取所属的 Profile 和观察代次；
- `ADAPTER`：哪个 TIP 适配器参与了解释、使用了哪些状态源，以及解释是否成功。

日志不能只写含混的 `value`、`previous` 或 `current`。字段名必须指出它是
`openValue`、`conversionValue` 还是语言栏状态，避免把不同来源的整数误认为同一种状态，
不得把字段名称混进自然语言，难以区分值来源。

## 最终认识

TSF 提供了 Profile、compartment、焦点、语言栏和能力分类等机制，但没有要求所有 TIP 使用
相同的输入模式模型，也没有要求 TIP 必须把内部输入状态发布到某一个统一的公共接口。IMM
窗口状态同样只是兼容路径，不是现代 TIP 的统一状态契约。

后续实验进一步证明，即使某个标准入口存在，也不能把“可查询”与“可信”画等号。
`GUID_LBI_INPUTMODE` 可以存在并成功返回图标和命令，却仍可能与系统托盘甚至实际输入状态
不同步。某些 TIP 则可能完全不更新 open/conversion，也不产生对应 Windows 消息。

因此，当前更准确的方向是：

> 使用 TSF 确认活动 Profile 身份，先探测 TIP 声明的能力和运行时实际暴露的对象，再由经过
> 实机验证的 ClassId/ProfileId 适配器选择可信状态源。基础库不再尝试为未知 TIP 推断模式，
> 也不提供通用模式切换键；只有进入兼容性白名单的 TIP 才开放该能力。长期通过自有完整
> TSF TIP 建立单一状态源和 reference implementation。
