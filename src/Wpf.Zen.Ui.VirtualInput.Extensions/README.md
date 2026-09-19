# 软键盘扩展库

本程序集为 `Wpf.Zen.Ui.VirtualInput` 提供显式注册的语言和输入法适配。
公开入口保留在 `Wpf.Zen.Ui.VirtualInput.Extensions` 命名空间，内部类型按目录划分命名空间。

| 目录 | 职责 | 主要类型 |
| --- | --- | --- |
| `Providers/` | 按语言注册能力，组合默认扩展 | `DefaultSoftKeyboardExtensionProvider`、中日扩展入口 |
| `InputMethods/` | 解释服务原始观察、生成按钮和执行描述、执行服务专用写入 | `ChineseInputModeAdapter`、`MicrosoftJapaneseInputModeAdapter` |
| `Layouts/` | 定义语言符号表，生成满足 InputScope 和页面约束的布局 | 中日 `CompactLayoutProvider`、共享的 `LanguageCompactLayoutProvider` |
| `Candidates/` | 按服务选择候选呈现策略 | `WindowsCandidatePresentationBehavior` |
| `Routing/` | 根据活动 Profile 语言或备用 Culture 匹配语言能力 | `ProfileLanguage` |
| `Properties/` | 程序集元数据 | `AssemblyInfo` |

## 命名边界

服务标识采用厂商与产品名称，例如 `MicrosoftPinyin` 指当前适配的微软拼音 TSF 服务，
不代表所有拼音输入法。微软注音、日语、仓颉、韩语等服务同样带 `Microsoft` 前缀；
`IFlytek`、`WeChat` 本身已明确厂商或产品。

语言层与服务层分别命名：`ChineseInputModeAdapter` 汇集多个已知中文服务的精确匹配规则，
`MicrosoftJapaneseInputModeAdapter` 专用于微软日语输入法；`JapaneseCompactLayoutProvider`
和日文扩展注册入口仍按语言命名。匹配语言只选择能力集合，实际解释还必须匹配服务标识。

## 依赖与职责

- `Providers` 只负责注册；中文、日文入口按 `Routing` 匹配语言，默认入口再追加候选策略。
- `InputMethods` 各自拥有服务值解释、键帽、回退命令，并引用 `Layouts` 的共享实例。
  日文适配器在可解释时设置 `OverridePlatformButton`；中文适配器提供回退按钮。
- `Layouts` 只依赖布局上下文和语言匹配。共享基类按网址、邮箱、数字、紧凑布局的顺序委托核心提供器；
  中日子类各自保存符号表，不负责读取输入法状态。
- `Candidates` 独立维护支持内嵌候选的服务集合；候选策略和模式解释共享 `KnownTextServices` 中的身份常量，
  各自决定支持范围。身份常量不代表服务具备某项能力。
- 核心拥有观察计时、语言栏查询、最终呈现优先级、点击路由与 30ms 延迟复查；
  扩展根据传入的不可变观察生成贡献，成功写入不预测后续界面。

中日适配器独立实现 `ISoftKeyboardInputModeAdapter`，没有汇总两种语言规则的共同适配器基类。
添加另一种语言时，在 `InputMethods` 和 `Layouts` 中实现其能力，在 `Providers` 中注册；
只增加已有语言的服务支持时，修改对应适配器的精确服务匹配和值解释。
候选支持单独评估，不随输入模式适配自动启用。

完整的观察时序、缺失值语义、布局优先级和能力边界见 [SoftKeyboard 文档](../../docs/SoftKeyboard.md)。
