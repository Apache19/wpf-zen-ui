# WPF Zen UI

[English](https://gitee.com/apachezy/wpfui/blob/main/README.md) | [简体中文](https://gitee.com/apachezy/wpfui/blob/main/README.zh-CN.md)

WPF controls, themes, navigation, and dialogs for Windows desktop applications.
This library is an independently maintained, refactored fork of
[WPF UI](https://github.com/lepoco/wpfui), not an official upstream release.

Version `1.0.0` is the first release under the `Wpf.Zen.Ui` package name.
Its version numbers are independent of upstream WPF UI. Namespaces and APIs have
been refactored; this package is not a drop-in replacement for upstream packages.

## Getting started

Install the package in your WPF application:

```shell
dotnet add package Wpf.Zen.Ui --version 1.0.0
```

The package supports .NET Framework 4.7.2 and .NET 8 for Windows, and compatible
later targets. Declare the library's XML namespace in a window or user control
and use its controls directly:

```xml
<Window
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <Grid>
        <ui:Button Content="Hello, WPF Zen UI" />
    </Grid>
</Window>
```

## Source and issues

Source code and issue tracking are available in the
[WPF Zen UI repository](https://gitee.com/apachezy/wpfui).

## License

Distributed under the [MIT License](https://gitee.com/apachezy/wpfui/blob/main/LICENSE).
The package includes `LICENSE` and `ThirdPartyNotices.txt`. Preserve applicable
copyright and permission notices when redistributing the library.
Third-party components and SDK-derived material retain their applicable upstream
terms; see `ThirdPartyNotices.txt` and `licenses/WindowsSdkLicense.txt` in the package.

## Original Repository

- **Project Name**: WPF UI
- **Repository**: [https://github.com/lepoco/wpfui](https://github.com/lepoco/wpfui)
- **Author**: [Leszek Pomianowski](https://wpfui.lepo.co/) and the open-source community
- **License**: MIT License

## Attributions

- Status colors follow the [GitHub Primer](https://github.com/primer/primitives) palette (MIT License, Copyright (c) 2018 GitHub Inc.)
- Embedded Fluent System Icons fonts are from
  [Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) (MIT License).
