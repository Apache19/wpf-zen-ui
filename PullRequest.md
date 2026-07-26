## 拉取请求类型

- [x] 新功能
- [x] 构建相关变更

## 当前行为是什么？

WPF UI Gallery 有两个独立的项目：
- `src/Wpf.Ui.Gallery/` — 完整功能，仅目标 `net10.0-windows10.0.26100.0`
- `src/Wpf.Ui.Gallery.NetFramework/` — 通过 linked files 复用代码，目标 `net462`，功能受限（无 i18n、无 Tray、无 SyntaxHighlight、无 Monaco）

两个项目需要独立维护、构建和测试，存在大量重复的 XAML 和 C# linked files。

## 新行为是什么？

将 `Wpf.Ui.Gallery` 改为多目标项目（`net10.0-windows10.0.26100.0;net462`），
两个目标共享同一套源代码，**功能完全一致**。

**关键发现：** 所有 Gallery 依赖的组件均已支持 net462，包括 Monaco（WebView2 编辑器）：
| 组件 | net462 支持 |
|---|---|
| `Microsoft.Extensions.Hosting` 10.0.9 | ✅ `lib/net462` |
| `Lepo.i18n.Wpf` 2.1.0 | ✅ `lib/net462` |
| `Lepo.i18n.DependencyInjection` 2.1.0 | ✅ `lib/net462` |
| `Wpf.Ui.Tray` | ✅ 多目标 `net8.0;net462` |
| `Wpf.Ui.SyntaxHighlight` | ✅ 多目标 `net8.0;net462` |
| `Wpf.Ui.DependencyInjection` | ✅ 多目标 `net8.0;net462` |

**具体变更：**
- `Directory.Packages.props`：`Lepo.i18n` 2.0.0 → 2.1.0（修复版本不匹配）
- `Wpf.Ui.Gallery.csproj`：改为 `TargetFrameworks` 多目标，添加 net462 专属构建支持和 SG 解决方案
- `WindowsViewModel.cs`：`#if NET10_0_OR_GREATER` 保护 Monaco 相关代码
- `FilePickerViewModel.cs`：`#if NET10_0_OR_GREATER` 处理 `File.WriteAllTextAsync` vs `File.WriteAllText`
- `PageControlDocumentation.xaml.cs`：修复 `string.Concat` 跨目标兼容性
- 删除 `Wpf.Ui.Gallery.NetFramework/` 整个项目（约 650 个文件）
- 从 `Wpf.Ui.slnx` 移除 NetFramework 项目引用

## 其他信息

- 两个 TFM 编译验证通过（`net462` 和 `net10.0-windows10.0.26100.0` 均为 0 错误），功能完全一致
- Monaco 编辑器、i18n 多语言、Tray 托盘图标、SyntaxHighlight 代码着色均在两个目标上可用
- Gallery Package (wapproj) 无需变更，仍引用主 Gallery 项目
