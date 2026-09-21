# MessageBox

`Wpf.Zen.Ui.Controls.MessageBox` 是静态入口，使用真正的 WPF 模态窗口。
`Show` 会运行模态消息循环，用户完成选择、窗口关闭之后才返回。
调用线程必须是 STA UI 线程；指定 owner 时必须与 owner 位于同一个 Dispatcher。

```csharp
using System.Windows;
using UiMessageBox = Wpf.Zen.Ui.Controls.MessageBox;

MessageBoxResult result = UiMessageBox.Show(
    this,
    "是否保存当前修改？",
    "未保存的修改",
    MessageBoxButton.YesNoCancel,
    MessageBoxImage.Question,
    MessageBoxResult.Cancel);

if (result == MessageBoxResult.Yes)
{
    Save();
}
```

提供与 WPF 对应的十二个 `Show` 重载，直接使用 `System.Windows` 中的
`MessageBoxButton`、`MessageBoxImage`、`MessageBoxResult` 和 `MessageBoxOptions`。
支持本库目标框架 .NET Framework 4.7.2 / .NET 8 的四种标准按钮组合：
OK、OKCancel、YesNo、YesNoCancel。

## 自定义按钮文字

在完整参数重载末尾传入 `MessageBoxButtonText`，可逐个替换按钮文字，返回值保持标准语义。

```csharp
MessageBoxResult result = UiMessageBox.Show(
    this, "是否保存当前修改？", "未保存的修改",
    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
    MessageBoxResult.Cancel, MessageBoxOptions.None,
    new Wpf.Zen.Ui.Controls.MessageBoxButtonText
    {
        Yes = "保存(_S)",
        No = "不保存(_N)",
        // Cancel 使用内置的本地化文字。
    });
```

null 或空字符串使用调用时 `CurrentUICulture` 对应的资源。
内置英语、简体中文、繁体中文、日语、德语和法语；其他语言按资源父文化链回退到英语。
下划线标记访问键，双下划线表示文字中的下划线。文字在每次调用时读取，不会影响其他消息框。

## 交互约定

- 默认按钮获得初始焦点；Enter、Tab 和访问键由 WPF 按钮处理。
- 未指定默认结果，或指定的合法结果不在当前组合中时，默认第一个按钮，与 WPF 一致。
- OK 的 Esc、关闭按钮、Alt+F4 返回 OK。
- 含 Cancel 的组合通过上述方式关闭时返回 Cancel。
- YesNo 禁止这些关闭方式，必须明确选择 Yes 或 No。
- 显式 owner 控制归属与居中；未指定 owner 时由 WPF 采用当前活动窗口。
- 模态期间 WPF 禁用当前线程的其他窗口，退出后恢复窗口和激活状态。
- Ctrl+C 复制标题、消息和按钮文字；长消息可滚动，按钮长文字可换行。
- RightAlign 和 RtlReading 分别控制正文对齐和阅读方向。
- ServiceNotification / DefaultDesktopOnly 交给原生 WPF MessageBox，使用系统外观；
  这些模式不能指定 owner，也不能使用自定义按钮文字。

## 结构

公开入口只负责参数与调用。内部 `MessageBoxWindow` 负责一次调用的内容、模态生命周期、
Fluent 标题栏、图标和按钮。FluentWindow 负责原生边框、窗口主题和 DPI 行为。

旧实例式 MessageBox、ShowDialogAsync、Primary/Secondary/Close 按钮模型和自定义结果枚举已移除。
Gallery 的 MessageBox 页面展示同步调用；ContentDialog 页面中的 PromptDialog 仍是宿主内异步提示框。

也可以使用命名参数省略其他默认参数：

```csharp
UiMessageBox.Show("操作完成。", buttonText: new MessageBoxButtonText { OK = "知道了" });
```

外观遵循库的主题资源；未配置主题时使用默认浅色外观。应用可通过现有的 ThemeManager 配置深色或系统主题。显式 owner 的 Theme.Requested 局部主题会应用于消息框。
