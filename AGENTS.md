## 宗旨
禁止生成“能用就行”的代码。任何实现都必须是可长期维护、功能完整的最终实现，而不是实验性草稿、最小可行版本或日后还需反复重构的半成品。

## 项目结构

- 控件库： `src\Wpf.Zen.Ui`
- 软键盘控件核心库： `src\Wpf.Zen.Ui.VirtualInput`
- 软键盘控件扩展库： `src\Wpf.Zen.Ui.VirtualInput.Extensions`
- 图库应用： `src\Wpf.Zen.Ui.Gallery`

## 源码参考指南

当遇到实现细节不明确、API 行为异常或需要理解底层逻辑时，**请优先查阅 WPF 官方源代码**。

本地 WPF 官方源码仓库位置：`E:\Repos\WPF\wpf`
CommunityToolkit: `E:\Repos\CommunityToolkit`
Windows classic samples:`E:\Repos\Windows-classic-samples`

通过阅读源码，可以获得最准确的实现依据，避免依赖猜测或不可靠的网络资料。这是解决复杂问题的首选途径。

## Win32 API 使用

如需要调用 Win32 API，则应使用 CsWin32 库，在项目根目录下的 NativeMethods.txt 里声明所需 API 并使之生成 PInvoke，不要自己单独实现 NativeMethods。

## 合理使用反射

由于 WPF 本身封装较深，部分框架能力、内部状态或默认行为无法通过公开 API 直接访问。
在确有必要时，可以合理使用反射读取 WPF 内部状态、调用内部方法，或复用 WPF 内部实现，以减少重复造轮子并提升实现质量。

## 大胆重构

本库正在进行彻底重构，修改源代码时不需要考虑旧 API 兼容、迁移成本，应主动识别历史包袱，并在合理范围内进行彻底整理。
本库更重视长期架构质量，而不是短期兼容性，当前目标是建立更清晰、更一致、更可维护的架构，而不是维持旧版本行为。
实现需求时，如果原有设计错误或架构缺陷成为阻碍，应果断进行彻底重构，不要为了坚持“最小修改”而牺牲代码质量。

## 编写详尽注释

- 编写较为详尽的 XML 文档注释和关键实现注释，使阅读者能够理解代码用途、行为边界、依赖关系和维护注意事项等，避免留下需要猜测的设计。

- 修改代码时应为缺少注释的旧代码补充 XML 文档和注释。

- 注释应说明代码的用途、行为、默认值、使用场景，以及必要的注意事项，不要只写重复名称和代码逻辑的无意义注释。

- 所有 `public`、`protected`、`protected internal` 的类型、属性、方法、事件、字段、依赖属性、附加属性、枚举值，原则上都应编写 XML 文档注释。
  即使是 `private` 方法，如果它承载了重要内容，也应添加简要注释说明其职责和边界。

- 注释应聚焦原始目标，不要把“为什么没有做某件本来就不该做的事”写进注释和文档。
  - 反例：用户质疑“番茄炒蛋有必要加东坡肉吗”，去掉东坡肉后注释里说明为什么这里不需要东坡肉。
  - 正确：注释里只说“番茄炒蛋”，不再提及东坡肉。
  核心：不把常识当契约，不把“没做错”当卖点。

- XML 文档注释的每一行（包含缩进和 `///` 前缀）最大长度为 100 个字符。
  `summary` 和 `remarks` 必须使用独立的开始标签、内容行和结束标签，禁止写成 `<summary>内容</summary>` 或 `<remarks>内容</remarks>` 的单行形式。
  `para`、`param` 和 `returns` 可在内容和标签合计不超过 100 个字符时保留单行；超过该上限时必须使用与 `summary` 相同的多行形式，并按自然语义断句或断词，避免超过行宽或形成难以阅读的长行。

## Issue 与 Pull Request 规范

**如果用户提供了 Issue 编号，则必须遵循此规范，否则请忽略此规范**

在完成问题修复后，请根据仓库根目录下的 `.gitee/pull_request_template.md` 或 `.github/pull_request_template.md` 模板，生成相应的 Pull Request（PR）内容。

生成的 PR 信息中，第一行请按以下格式设定标题：

```
类型：内容
```

其中，“类型”为提交类别的简要概括（如 `修复`、`功能`、`优化` 等），“内容”为该 PR 的核心目的说明。

生成完成后，请将完整的 PR 内容保存为 `PullRequest.md` 文件，存放于仓库根目录下，以便用户直接查阅或使用。

**注意事项：**
- 若后续根据用户反馈对修复方案进行了调整或补充，请同步更新 `PullRequest.md` 文件中的相关内容，确保其与最终修复方案保持一致。
- 更新后请告知用户文件已同步修改，便于用户及时获取最新版本。

## 代码风格

### 依赖属性和 CLR 包装属性

原仓库在书写依赖属性时习惯把依赖属性标识定义与对应的 CLR 包装属性分开放置，这在浏览代码时极为不便。
现不允许再使用这种风格，请把 CLR 包装属性与依赖属性定义放在一起，CLR 包装属性跟随依赖属性定义即可。

### 花括号

单行 if、for、foreach 等语句不得省略花括号。
- 反例：`if (a == 1) dosomething;`
- 正例：
  ```
  if (a == 1)
  {
      dosomething;
  {
  ```
### 方法表达式

单行方法不得写成表达式，必须写成完整方法体。
- 反例：`void Method() => DoSomething();`
- 正例：
  ```
  void Method()
  {
      DoSomething();
  }
  ```
**此事项只针对方法，单行属性和访问器仍可使用表达式形式**

### 禁止“一行流”代码

禁止把多个有独立含义的操作压缩在同一表达式中，例如连续进行类型转换、容器查找和方法调用，或在调用参数中嵌套复杂计算、条件选择与其他调用。
应按操作步骤拆分语句，使用能表达用途的局部变量保存中间结果，使读者能够顺序理解数据来源、转换过程和最终用途。
此规则也适用于属性访问器、条件表达式和跨多行书写的嵌套表达式；仅将长表达式换行不算完成整理。
简单的属性访问、依赖属性包装、单次调用和清晰的基础运算可以保留简洁形式，不必机械拆分。

- 反例：
  ```csharp
  (((ItemContainerGenerator)ItemContainerGenerator).ContainerFromIndex(index) as FrameworkElement)?.BringIntoView();
  ```
- 正例：
  ```csharp
  var generator = (ItemContainerGenerator)ItemContainerGenerator;
  var element   = generator.ContainerFromIndex(index) as FrameworkElement;
  element?.BringIntoView();
  ```

### 调试日志

日志输出应符合以下结构化日志风格：

`[Category] Message; key=value, key=value.`

- 固定来源前缀：使用 `[Category.SubCategory]` 标识日志来源，例如 `[SoftKeyboard.InputMode.Monitor]`。
- 消息部分：简单描述事件，简洁清晰；
- 关键领域词使用大写突出；
- 大写只用于“锚点词”：不是整句大写，而是只强调少量高价值关键词，这样既醒目，又不会降低整体可读性；
- 属性统一使用 key=value：多个属性之间使用逗号分隔，格式稳定，便于人工阅读和程序解析。

示例：
```
[SoftKeyboard.InputMode.Monitor] Profile activation observed; profileType=1, languageId=0x0804, classId=81d4e9c9-1d3b-41bc-9e6c-4b40bf79e35e, categoryId=34745c63-b2f0-4784-8b67-5e12c8701a31, profileId=fa550b04-5ad7-411f-a5ac-ca038ec515d7, keyboardLayout=0x0, flags=0x00000002, active=False.
[SoftKeyboard.InputMode.Monitor] Applying observed change; kind=Profile, observationVersion=1.
[SoftKeyboard.InputMode.Monitor] Profile deactivation processed; classId=81d4e9c9-1d3b-41bc-9e6c-4b40bf79e35e, profileId=fa550b04-5ad7-411f-a5ac-ca038ec515d7.
[SoftKeyboard.InputMode.Monitor] TSF CONVERSION compartment notification; compartment=GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION, conversionValue=1025 (0x00000401), readSucceeded=True.
[SoftKeyboard.InputMode.Monitor] Applying observed change; kind=ImeConversion, conversionValue=1025 (0x00000401), observationVersion=2.
```

## 构建

构建环节遇到输出文件被进程占用，请结束进程继续构建。