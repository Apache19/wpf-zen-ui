# WPF Zen UI

[English](https://github.com/Apache19/wpf-zen-ui/blob/main/README.md) | [简体中文](https://github.com/Apache19/wpf-zen-ui/blob/main/README.zh-CN.md)

面向 Windows 桌面应用的 WPF 控件、主题、导航与对话框库。
本库由 [WPF UI](https://github.com/lepoco/wpfui) 分支而来，经过重构并独立维护，
并非上游项目的官方发行版本。

`1.0.0` 是以 `Wpf.Zen.Ui` 包名发布的首个版本，版本号独立于上游 WPF UI。
命名空间和 API 已经过重构，不能直接替换上游包而不调整应用代码。

## 快速开始

在 WPF 应用中安装 NuGet 包：

```shell
dotnet add package Wpf.Zen.Ui --version 1.0.0
```

本包支持 .NET Framework 4.7.2、.NET 8 for Windows 及兼容的更高版本。
引用后，在窗口或用户控件中声明本库的 XML 命名空间，即可直接使用控件：

```xml
<Window
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <Grid>
        <ui:Button Content="你好，WPF Zen UI" />
    </Grid>
</Window>
```

## 源码与问题反馈

源码和问题跟踪位于 [WPF Zen UI 仓库](https://github.com/Apache19/wpf-zen-ui)。

## 许可证

本库采用 [MIT 许可证](https://github.com/Apache19/wpf-zen-ui/blob/main/LICENSE) 分发。
NuGet 包包含 `LICENSE` 和 `ThirdPartyNotices.txt`，再分发时请保留适用的版权和许可声明。
第三方组件及源自 SDK 的材料仍适用各自的上游条款，
详见包内的 `ThirdPartyNotices.txt` 与 `licenses/WindowsSdkLicense.txt`。

## 上游项目

- **项目名称**：WPF UI
- **仓库**：[lepoco/wpfui](https://github.com/lepoco/wpfui)
- **作者**：[Leszek Pomianowski](https://wpfui.lepo.co/) 及开源社区
- **许可证**：MIT License

## 致谢与第三方资源

- 状态配色参考 [GitHub Primer](https://github.com/primer/primitives) 调色板
  （MIT License，Copyright (c) 2018 GitHub Inc.）。
- 内嵌的 Fluent System Icons 字体来自
  [Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)
  （MIT License）。
