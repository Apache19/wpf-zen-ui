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
| 讯飞输入法 | `{B722B5D7-0C4C-4933-A7B9-DF8C91F2C643}` | `open = 2`，`conversion = 0` | `open = 1`，`conversion = 0` | conversion 未观察到参与模式区分。 |
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

## 架构结论

### 可以通用化的部分

基础设施可以统一负责采集原始信号：

- 活动 Profile、ClassId 和 ProfileId；
- open compartment 的原始值及其通知；
- conversion compartment 的原始值及其通知；
- `GUID_LBI_INPUTMODE` 语言栏项目及其更新；
- WPF 焦点和 TSF 文档焦点变化；
- 每次观察所属的 Profile、焦点文档和状态代次。

基础设施只应陈述“观察到了什么”，不应赋予原始整数跨输入法的通用含义。

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
8. 未知 TIP 优先使用其标准语言栏项目；无法可靠解释时，应保持未知或不提供切换，
   而不是猜测。

## 状态收敛与原子发布方案

输入模式观察应从“每条通知立即刷新界面”改为“收集一个稳定快照后原子发布”。建议采用
以下状态过程：

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

延迟与收敛机制只能帮助等待 TIP 完成初始化，不能创造 TIP 从未提供的状态。达到截止时间
后仍无法可靠解释时，应隐藏模式切换按钮；未知 TIP 可以在有效语言栏按钮存在时使用它，
否则保持无模式切换能力。

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

TSF 提供了 Profile、compartment、焦点和语言栏等机制，但没有要求所有 TIP 使用相同的
输入模式模型。系统语言栏能够准确显示某个 TIP 的状态，是因为 TIP 自己拥有并发布相应的
呈现；外部控件仅凭标准 compartment 无法完整复制这种内部知识。

因此，本项目的长期方案应当是：

> 通用地观察原始信号，在 Profile 激活后等待状态收敛，由扩展按输入法解释同一份快照，
> 最后原子发布布局、模式和切换按钮。
