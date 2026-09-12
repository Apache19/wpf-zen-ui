// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.Settings;

/// <summary>
/// An immutable settings-menu node with presentation slots and an optional dispatcher-bound action.
/// </summary>
/// <remarks>
/// Nodes contain data, not menu controls. An empty group renders as an ordinary command; Separator
/// nodes render as separators. RadioGroup is local to one menu instance. Providers may use record
/// copies to translate headers or supply icon data and styles without replacing built-in actions.
/// </remarks>
public sealed record SoftKeyboardSetting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardSetting"/> class.
    /// </summary>
    /// <param name="id">Stable identifier for customization and diagnostics.</param>
    /// <param name="header">Header data, resolved through WPF content templates.</param>
    /// <param name="apply">Optional action applied after the menu closes.</param>
    /// <param name="children">Optional submenu nodes, copied immediately.</param>
    public SoftKeyboardSetting(
        string id,
        object? header = null,
        Action<SoftKeyboard>? apply = null,
        IEnumerable<SoftKeyboardSetting>? children = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "A setting identifier is required.",
                nameof(id)
            );
        }

        Id       = id;
        Header   = header;
        Apply    = apply;
        Children = [with([.. children ?? []])];

        if (Children.Any(child => child is null))
        {
            throw new ArgumentException("A child cannot be null.", nameof(children));
        }
    }

    /// <summary>
    /// Gets the stable identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the header data; record copies may replace it for localization.
    /// </summary>
    public object? Header { get; init; }

    /// <summary>
    /// Gets optional icon data, such as a FluentIconSource. Each menu creates its own presenter.
    /// </summary>
    public object? Icon { get; init; }

    /// <summary>
    /// Gets an optional MenuItem/RadioMenuItem/Separator style appropriate to this node.
    /// </summary>
    public Style? ItemStyle { get; init; }

    /// <summary>
    /// Gets the submenu snapshot.
    /// </summary>
    public ReadOnlyCollection<SoftKeyboardSetting> Children { get; }

    /// <summary>
    /// Gets the optional action. Actions must not retain an editor or perform asynchronous input.
    /// </summary>
    public Action<SoftKeyboard>? Apply { get; }

    /// <summary>
    /// Gets a local radio group identifier, or null for an ordinary node.
    /// </summary>
    public string? RadioGroup { get; init; }

    /// <summary>
    /// Gets a value indicating whether this radio choice is selected in the captured state.
    /// </summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the action is available; defaults to true.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether this node represents a separator rather than a command.
    /// </summary>
    public bool IsSeparator { get; init; }
}