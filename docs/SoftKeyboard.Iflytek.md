# 讯飞输入法适配记录

验证环境：Windows 11、.NET 10.0.12、讯飞输入法 3.0.1750（x64），2026-09-14。

## 状态与切换

本机 TSF profile 枚举确认，讯飞中文输入服务的 CLSID 为
`B722B5D7-0C4C-4933-A7B9-DF8C91F2C643`。
在实际中文输入时，`GUID_COMPARTMENT_KEYBOARD_OPENCLOSE` 返回整数 `2`，英文模式返回 `1`；
`GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` 保持为 `0`。
因此 WPF `InputMethod.ImeState` 会把两种模式都读成 `On`，而将其与 `Native` 转换标志组合又会
把中文误判成 `Off`。普通 WPF 状态写入可以改变 compartment，却不足以改变讯飞引擎的输入行为。

专用扩展保留原始整数语义，通过当前线程语言栏的 `GUID_LBI_INPUTMODE` 项，调用
`ITfLangBarItemButton.OnClick(TF_LBI_CLK_LEFT, ...)` 完成引擎切换。
按钮文字和工具提示在两种模式下均可能相同，因此不用本地化字符串推断状态。
软键盘不会把该命令包装成绝对的 Boolean 写入；适配器读取并解释当前模式，命令只请求一次
由讯飞定义的转换，显示结果仍通过后续 TSF 观察回读发布。

从软键盘菜单选择输入法还有独立的焦点时序问题：菜单未关闭时激活讯飞，随后菜单退出和
编辑器焦点恢复会使读取到的 compartment 变成 `1`，实际引擎仍可输入中文。
原生菜单测试在三个候选模式均复现了这一差异。菜单命令现在先关闭浮层，等待
`DispatcherPriority.ContextIdle`，恢复并验证编辑器焦点后再激活 profile。
延迟请求绑定输入会话、目标和菜单请求版本，关闭键盘、更换来源或新的菜单请求都会使旧请求失效。

厂商规则位于 `Wpf.Zen.Ui.VirtualInput.Extensions`。核心的 `SoftKeyboardTsfInputMode`
只提供原始 TSF 状态读取与标准语言栏命令，不包含讯飞的 GUID 或状态映射。
宿主使用 `DefaultSoftKeyboardExtensionProvider.Default` 或显式组合讯飞扩展即可启用；Gallery 已使用默认组合。

## 首次进入页面的候选显示

原候选桥接会抢在 WPF 注册首个 TextStore 前调用
`ActivateEx(TF_TMAE_UIELEMENTENABLEDONLY)`。该标志限制 TSF 激活的输入法类别，导致页面进入顺序
影响第三方输入法的初始化。桥接现在只借用 WPF 已初始化的 ThreadManager，不再额外激活或停用它。
其它支持内嵌的输入法仍通过 UIElement 接口接管。是否回退由实际候选接口能力决定，具体边界见下文。

相关系统契约：
[ITfThreadMgrEx.ActivateEx](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfthreadmgrex-activateex)、
[ITfLangBarItemButton.OnClick](https://learn.microsoft.com/en-us/windows/win32/api/ctfutb/nf-ctfutb-itflangbaritembutton-onclick)。
实现同时对照本地 WPF 的 `InputMethod.cs`、`TextServicesCompartment.cs`、`TextServicesHost.cs`，
以及 Windows SDK 的 `msctf.h`、`ctfutb.h`。

## 崩溃转储

读取本机已有转储，没有修改系统崩溃收集配置：

- `Wpf.Zen.Ui.Gallery.exe.12116.dmp`：`MSCTF!CEnumUnknown::~CEnumUnknown` 读取失效的接口指针，
  下层调用来自 `CoreCLR!RCW::ReleaseAllInterfaces`、`RCW::Cleanup`。
- `Wpf.Zen.Ui.Gallery.exe.25996.dmp`：`CoreCLR!SafeReleasePreemp` 在相同 RCW 清理路径失败，
  最终进入 `EEPolicy::HandleFatalError`。
- 后续 `Wpf.Zen.Ui.Gallery.exe.1420.dmp`：RCW 清理时触发间接调用保护失败。
- 后续 `Wpf.Zen.Ui.Gallery.exe.24716.dmp`：RCW 清理进入 `iFlyTsfIme.dll` 的释放函数，
  最终在 `RtlFreeHeap` 报告 `0xc0000374` 堆损坏。

这说明日志记录的是原生 COM 清理阶段的访问违规；托管 `try/catch` 不能修复已经失效的接口指针。
最初只对候选枚举器使用 `finally` 及时释放，未解决持续中文输入时的崩溃；此前交替切换中英文的
短时测试没有覆盖这一场景。补充持续输入后，第一轮合成后的 GC 即复现 `0x80131506`。
停用候选对象探测的对照运行完成 100 轮连续中文输入，没有崩溃。

上一版曾用 profile 级开关完全跳过讯飞候选探测；这只能绕过触发路径，不能说明输入法的实际能力，
现已删除 `SoftKeyboardExtension.UseNativeCandidateWindow` 及讯飞的强制回退配置。

## 通用能力判断与引用释放

本轮在正常 WPF TSF 线程上记录到：

- `QueryInterface(ITfCandidateListUIElement)` 和候选操作接口均成功。
- `GetDocumentMgr` 返回 `0x80004001`（`E_NOTIMPL`），文档输出为 null。
- WPF ThreadManager 的 `GetFocus` 返回成功，并提供非空焦点文档。

因此“能转换成候选接口”不等于可以接管。桥接无法把该候选绑定到当前编辑目标，必须保留原生 UI。
用户确认系统触摸键盘面对讯飞也显示原生候选浮窗。通用判断不依赖讯飞的 CLSID、版本或显示名称：
候选接口、操作接口或文档关联不可用时，自动保留原生呈现。

原探测路径在文档检查失败后直接 `continue`，只释放枚举器，未同步释放已经获得的 UIElement RCW。
这些被拒绝的候选并未进入活动候选会话，却一直保留到 GC；清理可能发生在合成结束或后续输入期间。
现在 `GetUIElement` 与枚举路径都明确区分临时探测引用和会话接管引用，通过 `finally` 平衡每次
未被接管的获取。释放在 TSF Dispatcher 上同步完成；WPF 共享 ThreadManager 不参与这项释放。
这是适用于所有输入法的引用生命周期修正，不是讯飞特判。

另按微软 UI-less 文档修正通知时序：`BeginUIElement` 只协商显示与确认能力，不立即读取候选字符串和
数量；收到首次 `UpdateUIElement` 后才读取内容并启动后续刷新。
参见 [UI-less mode overview](https://learn.microsoft.com/en-us/windows/win32/tsf/uiless-mode-overview)。

证据边界：现有转储能定位到 RCW 清理及原生释放路径，原生引用计数测试能证实原实现的探测引用
延迟释放问题；这些证据不能证明讯飞内部堆内存最初在哪条指令被破坏，也不能据此认定标准 COM
允许输入法在仍有外部引用时销毁对象。完整的原生内存损坏因果链仍需要更早的现场或堆跟踪。
本轮带接口日志的反向对照没有稳定复现原生崩溃，其中一次前台输入受用户键入干扰，不能计为反证或通过。

## 验证

讯飞原生测试使用统一的自动候选协商，检查：

- 软键盘开关切换后实际输入 `a`，而非仅检查属性值。
- 从语言栏命令改变模式后，键面状态自动更新。
- 输入 `nihao` 后提交 `你好`。
- 在组合期间和提交后强制 GC，并泵送 STA 消息以执行 COM 清理。
- 从真实软键盘输入法菜单切换到讯飞，未经开关修正，先验证键面模式与实际输入一致。
  输入法可能记住英文状态；核对后仅在持续测试开始前设为中文。
- 保持中文模式连续完成 60 轮 `nihao` 合成和 `你好` 提交，累积正文，不在轮次之间切换模式或清空编辑器。
- 每轮合成中检查候选呈现状态为通用能力判断产生的 `NativeFallback`。

普通回归测试另行覆盖首次编辑器初始化前不抢占 TSF、非 Boolean 模式映射、按服务 GUID 匹配扩展，
以及 profile 切换时第一轮观察就使用新 profile 的适配器、跨越 profile 变化的模式读取被丢弃重试、
菜单关闭后激活和延迟请求失效。
新增回归使用带真实原生 vtable 的可计数 COM 对象，分别覆盖无候选接口和文档方法返回 E_NOTIMPL，
验证回调与枚举两条路径都在返回前恢复引用余额，不依赖 GC。另验证 Begin 不读内容、Update 才读取。
以下为删除候选模式设置前的诊断验证记录（当时仍有三种模式）：
反向对照只停用临时对象释放，四项原生引用计数用例全部失败：文档查询返回 E_NOTIMPL 的对象在
查询结束后仍有 3 个引用，而初始余额为 1。恢复释放并重新构建后，28 项生命周期回归全部通过。
本轮软键盘回归测试通过。仅增加未接管对象及时释放的初步对照完成三个模式各 60 轮持续输入，未崩溃。
最终持续输入测试三个模式均通过，共 180 轮，期间不切换中英文、不清空正文，并检查实际文字与原生回退状态。
日语、注音的 4 项以及讯飞开关的 3 项也通过；小狼毫 2 项因未安装跳过。
有一轮旧测试在输入前固定要求菜单切入后为中文而失败。测试现先核对报告模式与实际输入是否一致，
再准备持续中文输入，避免将输入法记忆的英文模式当成错误，也不会掩盖“A 状态实际输入中文”的问题。
Gallery 的 net10.0-windows 与 net472 构建通过，保留仓库已有的无关警告。
自动测试验证输入行为与清理路径，不等同于对所有讯飞版本的原生浮窗外观或候选接口能力作保证。

原生测试需要前台桌面，会注入按键。可在仓库根目录使用 PowerShell 执行：

```powershell
$env:WPF_ZEN_NATIVE_IFLYTEK_TESTS = '1'
dotnet run --project tests/Wpf.Zen.Ui.UnitTests/Wpf.Zen.Ui.UnitTests.csproj -- `
  -method '*Iflytek_*' -reporter verbose
```

首次进入场景应使用全新的测试进程；普通单元测试默认跳过这些原生交互用例。
