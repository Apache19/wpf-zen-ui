# WPF Mechanism Refactoring Roadmap

This roadmap tracks work to move Wpf.Ui back toward idiomatic WPF mechanisms:
styles, templates, attached properties, resource dictionaries, and opt-in
application-level standard control styling.

## Guiding Principles

- [ ] Keep a Wpf.Ui control type only when it adds a real behavior contract,
      state model, service integration, or interaction pattern that native WPF
      controls cannot express cleanly through styles and templates.
- [ ] Use native WPF controls plus styles for visual-only changes.
- [ ] Use attached properties for reusable template parameters such as icons,
      descriptions, placeholder text, corner radius, badges, and compact visual
      flags.
- [ ] Do not silently apply Wpf.Ui styles to native WPF controls globally.
      Global native-control styling must be explicit and opt-in.
- [ ] Treat `ThemeManager.UseStandardControlStyles` as the public switch for
      merging native WPF control styles into `Application.Resources`.
- [ ] Keep control styles, native standard styles, theme resources, and helper
      attached properties in separate resource/code areas.
- [ ] Preserve user-defined styles, templates, and local values. Library helpers
      should not overwrite explicit user choices.

## Resource And Theme Structure

- [ ] Separate Wpf.Ui custom control styles from native WPF control styles.
- [ ] Keep native WPF implicit styles out of `Generic.xaml` unless they are only
      needed for Wpf.Ui control templates.
- [ ] Move native WPF implicit styles intended for application-wide use into the
      `Standard.xaml` opt-in path.
- [ ] Make `UseStandardControlStyles` merge only the native standard style pack.
- [ ] Ensure standard style dictionaries are safe to merge at application scope.
- [ ] Avoid loader dictionaries that apply styles through reflection or other
      hidden global side effects.
- [ ] Delete or obsolete compatibility loaders once no shipped XAML references
      them.

## Attached Property Layer

- [ ] Design a small attached-property surface for shared visual metadata.
- [ ] Add an icon attached property usable by native `Button`, `MenuItem`,
      `ComboBoxItem`, `ListBoxItem`, and similar controls.
- [ ] Add optional attached properties only when multiple controls can share
      them.
- [ ] Prefer one focused helper type per domain, such as `IconAssist`,
      `TextBoxAssist`, or `MenuItemAssist`, over a large miscellaneous bag.
- [ ] Document which attached properties are stable public API.
- [ ] Add style examples that bind template parts to attached properties.

## Controls To Review

- [ ] `MenuItem`: remove the custom control type if icon support can be handled
      by an attached property and native `MenuItem` styles.
- [ ] `Menu`: keep only if it adds behavior beyond native `Menu`; otherwise move
      styling to native `Menu` styles.
- [ ] `ContextMenu`: prefer native `ContextMenu` styles; keep `ui:ContextMenu`
      only if it adds meaningful behavior.
- [ ] `TextBox`: split visual styling from behavior such as clear button,
      placeholder, and text-selection modes.
- [ ] `ComboBox`: separate editable text box styling from ComboBox behavior.
- [ ] `PasswordBox`: keep custom behavior only where command handling or reveal
      features require it; style native behavior through standard styles where
      possible.
- [ ] `DatePicker` and `TimePicker`: remove custom context menu setters and
      review whether native editable parts can use standard text styles.
- [ ] `NumberBox`: decide which parts are real behavior and which are standard
      text/input styling.
- [ ] `DataGrid`: separate native editing control styles from Wpf.Ui-specific
      DataGrid behavior.

## Context Menu Work

- [ ] Remove explicit Copy/Cut/Paste `ContextMenu` setters from editable controls
      where native WPF editing menus should be used.
- [ ] Let WPF text editing controls keep their native context menu behavior so
      keyboard Menu key placement follows the caret.
- [ ] Style native `ContextMenu`, `MenuItem`, and `Separator` through resource
      dictionaries instead of runtime reflection.
- [ ] Keep native context menu styles opt-in for application-wide WPF controls
      through `UseStandardControlStyles`.
- [ ] Verify mouse right-click and keyboard Menu key placement for `TextBox`,
      editable `ComboBox`, `RichTextBox`, `PasswordBox`, and DataGrid editors.

## Compatibility And Migration

- [ ] Provide migration notes from `ui:*` visual-only controls to native controls
      plus Wpf.Ui styles and attached properties.
- [ ] Keep obsolete wrapper controls temporarily when removing them would be a
      breaking change.
- [ ] Mark visual-only wrapper controls as obsolete only after replacement styles
      and attached properties are ready.
- [ ] Add examples for both library custom controls and native WPF controls with
      opt-in standard styling.
- [ ] Update gallery samples gradually so they demonstrate the preferred native
      control path where appropriate.

## Verification Checklist

- [ ] Build `src/Wpf.Ui/Wpf.Ui.csproj` for all target frameworks.
- [ ] Test resource lookup with and without `Application.Current`.
- [ ] Test resource lookup with and without `UseStandardControlStyles`.
- [ ] Test controls inside `Popup`, `ContextMenu`, `Frame`, and hosted WinForms
      `ElementHost` scenarios.
- [ ] Check keyboard navigation, focus visuals, automation peers, and command
      routing after replacing wrapper controls.
- [ ] Confirm user-provided styles and local property values still win over
      library defaults.
