// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Describes one immutable key shown by a <see cref="SoftKeyboard"/> .
/// </summary>
public sealed class SoftKeyboardKey
{
    /// <summary>
    /// Creates an unpositioned literal key suitable for adding to a layout builder row.
    /// </summary>
    /// <param name="text">The literal text, also used for the visible cap and automation name.</param>
    /// <returns>A text key using the keyboard's guarded Unicode input pipeline.</returns>
    public static SoftKeyboardKey FromText(string text)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, text, text, text: text);
    }

    /// <summary>
    /// Creates an unpositioned page-navigation key for a built-in or custom page.
    /// </summary>
    /// <param name="page">Destination page identifier.</param>
    /// <param name="content">Presentation data for the key cap.</param>
    /// <param name="automationName">Accessible description of the destination.</param>
    /// <returns>A navigation key that preserves composition cancellation.</returns>
    public static SoftKeyboardKey Navigate(string page, object content, string automationName)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.ChangePage, content, automationName, page: page);
    }

    /// <summary>
    /// Creates an unpositioned key with an application or language-specific function.
    /// </summary>
    /// <param name="action">Reusable custom action receiving guarded keyboard services.</param>
    /// <param name="content">Presentation data for the key cap.</param>
    /// <param name="automationName">Accessible description of the function.</param>
    /// <returns>A custom key with repetition disabled by default.</returns>
    public static SoftKeyboardKey FromAction(ISoftKeyboardAction action, object content, string automationName)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Custom, content, automationName, action: action);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardKey"/> class.
    /// </summary>
    /// <param name="kind">The semantic action performed by the key.</param>
    /// <param name="content">
    /// The content displayed on the key. Strings are rendered as ordinary text; data objects such
    /// as <see cref="Wpf.Zen.Ui.Controls.IconSource"/> are resolved through WPF data templates.
    /// </param>
    /// <param name="automationName">The descriptive name exposed to UI Automation.</param>
    /// <param name="key">The WPF virtual key for <see cref="SoftKeyboardKeyKind.VirtualKey"/>.</param>
    /// <param name="text">The Unicode value for <see cref="SoftKeyboardKeyKind.Text"/>.</param>
    /// <param name="page">The destination for <see cref="SoftKeyboardKeyKind.ChangePage"/>.</param>
    /// <param name="row">
    /// The nonnegative logical row identifier. Identifiers group and order keys; skipped identifiers
    /// do not reserve empty visual rows.
    /// </param>
    /// <param name="column">The zero-based logical column at which the key begins.</param>
    /// <param name="columnSpan">The positive number of logical columns occupied by the key.</param>
    /// <param name="alternates">Optional long-press Unicode alternatives.</param>
    /// <param name="isRepeatEnabled">
    /// Whether holding this key repeats its primary action. Defaults to false; alternatives take precedence.
    /// </param>
    /// <param name="alternateHint">
    /// Optional key-cap text previewing selected alternatives; null hides the hint. This is presentation only and does
    /// not define selectable values.
    /// </param>
    /// <param name="action">
    /// Required for a Custom key and forbidden for other kinds. Must not retain an editor or execution context.
    /// </param>
    /// <exception cref="ArgumentException">The key lacks presentation, accessible naming or input data,
    /// or an alternative is null or empty. Whitespace is valid input when its cap has a descriptive label.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind, virtual key or placement is invalid.</exception>
    public SoftKeyboardKey(
        SoftKeyboardKeyKind kind,
        object content,
        string automationName,
        Key key = Key.None,
        string? text = null,
        string page = SoftKeyboardPages.Letters,
        int row = 0,
        int column = 0,
        int columnSpan = 1,
        IEnumerable<string>? alternates = null,
        bool isRepeatEnabled = false,
        string? alternateHint = null,
        ISoftKeyboardAction? action = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (content is string textContent && string.IsNullOrWhiteSpace(textContent))
        {
            throw new ArgumentException(
                "Text key content cannot be empty.",
                nameof(content)
            );
        }

        if (string.IsNullOrWhiteSpace(automationName))
        {
            throw new ArgumentException(
                "An automation name is required.",
                nameof(automationName)
            );
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (columnSpan <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnSpan));
        }

        if (!Enum.IsDefined(typeof(SoftKeyboardKeyKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if ((kind == SoftKeyboardKeyKind.Custom) != (action is not null))
        {
            throw new ArgumentException(
                "Only custom keys must supply an action.", nameof(action)
            );
        }

        // Validate executable payloads at the immutable model boundary, before a provider's layout
        // reaches a key cap. A visible but nonfunctional key must not silently enter the input path.
        if (kind == SoftKeyboardKeyKind.VirtualKey
            && (!Enum.IsDefined(typeof(Key), key) || KeyInterop.VirtualKeyFromKey(key) <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        if (kind == SoftKeyboardKeyKind.Text && string.IsNullOrEmpty(text))
        {
            throw new ArgumentException(
                "A text key requires nonempty input text.",
                nameof(text)
            );
        }

        var alternateSnapshot = (alternates ?? []).ToArray();
        if (alternateSnapshot.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException(
                "Alternatives must contain nonempty input text.",
                nameof(alternates)
            );
        }

        Action         = action;
        Kind           = kind;
        Content        = content;
        AutomationName = automationName;
        Key            = key;
        Text           = text;

        Page = !string.IsNullOrWhiteSpace(page)
            ? page
            : throw new ArgumentException("A page identifier is required.", nameof(page));

        Row             = row;
        Column          = column;
        ColumnSpan      = columnSpan;
        Alternates      = new ReadOnlyCollection<string>(alternateSnapshot);
        IsRepeatEnabled = isRepeatEnabled;
        AlternateHint   = string.IsNullOrWhiteSpace(alternateHint) ? null : alternateHint;
    }

    /// <summary>
    /// Gets the action performed by the key.
    /// </summary>
    public SoftKeyboardKeyKind Kind { get; }

    /// <summary>
    /// Gets the custom action, or null for a built-in key kind.
    /// </summary>
    /// <remarks>
    /// The action is reusable behavior, not mutable per-keyboard state. Execution receives a short-lived context.
    /// </remarks>
    public ISoftKeyboardAction? Action { get; }

    /// <summary>
    /// Gets the visual content presented on the key.
    /// </summary>
    /// <remarks>
    /// This slot is designed for reusable presentation data rather than a shared visual element.
    /// Values such as <see cref="Wpf.Zen.Ui.Controls.FluentIconSource"/> allow the keyboard's content presenter to
    /// create an independent visual for every key. Custom layout providers may supply their own
    /// data objects together with an implicit <see cref="System.Windows.DataTemplate"/>.
    /// </remarks>
    public object Content { get; }

    /// <summary>
    /// Gets the descriptive UI Automation name.
    /// </summary>
    public string AutomationName { get; }

    /// <summary>
    /// Gets the WPF virtual key, or <see cref="Key.None"/> for a non-virtual key action.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Gets the Unicode text inserted by a text key.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the destination surface selected by a display-mode key.
    /// </summary>
    public string Page { get; }

    /// <summary>
    /// Gets the nonnegative logical row identifier consumed by the default keyboard item-container style.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the zero-based logical column at which the key begins. Keys are not automatically flowed
    /// after one another; layout providers assign this coordinate explicitly.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the positive number of equal-width logical columns occupied by the key.
    /// </summary>
    public int ColumnSpan { get; }

    /// <summary>
    /// Gets the long-press Unicode alternatives.
    /// </summary>
    public IReadOnlyList<string> Alternates { get; }

    /// <summary>
    /// Gets the optional preview displayed above the primary key content; defaults to null.
    /// </summary>
    /// <remarks>
    /// Providers may preview only a subset of alternatives to keep caps readable. The default template
    /// reserves a separate text row when this hint is present, so it cannot overlap the primary content.
    /// The hint never changes the primary action or the complete long-press list in <see cref="Alternates"/>.
    /// Whitespace-only hints are normalized to null. Custom templates may omit this presentation slot.
    /// </remarks>
    public string? AlternateHint { get; }

    /// <summary>
    /// Gets a value indicating whether the key permits repeated primary requests while held.
    /// </summary>
    /// <remarks>
    /// The layout provider owns this policy.
    /// </remarks>
    public bool IsRepeatEnabled { get; }

    // Provider helpers position semantic key descriptions by cloning them. This keeps the public
    // model immutable and prevents a shared key instance from acquiring mutable placement state.
    internal SoftKeyboardKey WithPlacement(int row, int column, int columnSpan)
    {
        return new SoftKeyboardKey(
            Kind,
            Content,
            AutomationName,
            Key,
            Text,
            Page,
            row,
            column,
            columnSpan,
            Alternates,
            IsRepeatEnabled,
            AlternateHint,
            Action
        );
    }
}
