# WPF Zen UI

This library is a refactored fork of [WPF UI](https://github.com/lepoco/wpfui).

## Text selection rendering

Call `Wpf.Zen.Ui.WpfCompatibility.EnableModernTextSelectionRendering()` at the beginning of your
application's `Main`, before creating controls, initializing themes, or loading XAML resources.
For WPF applications with `App.xaml`, use an explicit entry point and set `StartupObject` to its
type, as shown in the gallery and Simple example. Call the API before constructing `App` and
calling `InitializeComponent`; the `Startup` event can be too late.

This enables WPF's built-in selection background beneath text for all `TextBox` and `PasswordBox`
controls in the host. Their default selection opacity becomes 1; explicit opacity and brush alpha
still apply. `RichTextBox` is unaffected. The renderer requires .NET Framework 4.7.2 or modern .NET;
custom selection foregrounds via `SelectionTextBrush` require .NET Framework 4.8 or modern .NET.
WPF caches this setting, so the API is for startup configuration, not switching renderers at runtime.

`WpfCompatibility.IsModernTextSelectionRenderingEnabled` reports whether the host explicitly requests
the new renderer, including through runtime configuration. It is `false` when no switch is configured.
Styles can read it with an `x:Static` source in a one-time `DataTrigger` binding. The library's `TextBox`
style uses this to apply `AccentTextSecondary` as the selection brush only when enabled; otherwise it
leaves the selection brush unset. Configure the switch before loading styles, since `x:Static` captures
the value when XAML loads and does not track later changes.

## Documentation

- [Font icons and icon presentation](docs/FontIcon.md)
- [Navigation, semantic routes, and breadcrumbs](docs/Navigation.md)
- [ContentDialog and modal content](docs/ContentDialog.md)
- [ComboBox selection and text search](docs/ComboBox.md)
- [Snackbar notifications](docs/Snackbar.md)

## Original Repository

- **Project Name**: WPF UI
- **Repository**: [https://github.com/lepoco/wpfui](https://github.com/lepoco/wpfui)
- **Author**: [Leszek Pomianowski](https://wpfui.lepo.co/) and the open-source community
- **License**: MIT License

## Attributions

- Status colors follow the [GitHub Primer](https://github.com/primer/primitives) palette (MIT License, Copyright (c) 2018 GitHub Inc.)
