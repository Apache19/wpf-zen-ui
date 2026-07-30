# Migration plan

This page outlines key changes and important details to consider when migrating. It highlights what’s new, what’s changed, and any steps you need to take to ensure a smooth transition. This isn’t a full step-by-step guide but a quick reference to help you navigate the most critical parts of the migration process.

## Navigation

All navigation controls have been merged into one `NavigationView`, inspired by Win UI.

## Icons

Version 3 introduced an `IconElement` hierarchy for `TitleBar`, `NavigationView`, `Button`, and other controls. The current API has since returned those icon properties to ordinary WPF `object` content slots; use `SymbolIcon`, a native element such as `TextBlock`/`Image`, or non-visual data with a `DataTemplate`. `SymbolRegular` has a library-provided implicit data template, so assigning an enum value to any ordinary content presenter creates an independent `SymbolIcon`; a nearer application template can override that default through normal WPF resource lookup.

Use the explicit `{ui:Symbol Fluent24}` markup extension to assign symbol data in XAML, including control properties, styles, and resources. Icon properties deliberately remain typed as `object`, so a bare value such as `Icon="Fluent24"` is ordinary text rather than a context-sensitive symbol name. The extension does not construct or share a visual element: it returns a `SymbolRegular` value, and the implicit data template creates the visual for each presenter. A bound view-model property should likewise expose `SymbolRegular` rather than a symbol-name string.

## Control Gallery

Inspired by **Win UI Controls Gallery**, a new application for testing and browsing controls was created -** WPF UI Gallery**. It replaced the Demo app.

## Dialogs

`ContentDialog`, `MessageBox` and `Snackbar` have been modified, their interfaces are not fully compatible.
