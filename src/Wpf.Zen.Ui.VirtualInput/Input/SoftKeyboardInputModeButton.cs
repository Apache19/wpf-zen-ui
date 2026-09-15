// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Describes the presentation of the active input method's mode command.
/// </summary>
/// <remarks>
/// The descriptor presents a mode command without claiming to interpret a service-specific
/// conversion mode. The Windows source creates it from the active TSF language-bar item or a
/// supported keyboard-open fallback, while an input-mode adapter can replace it with a
/// service-specific descriptor. A null descriptor means that no reliable command should be
/// displayed.
/// </remarks>
public sealed class SoftKeyboardInputModeButton : IEquatable<SoftKeyboardInputModeButton>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputModeButton"/> class.
    /// </summary>
    /// <param name="id">
    /// A stable identifier for this presentation. Change it whenever the visual content changes.
    /// </param>
    /// <param name="content">Reusable content displayed on the mode command key.</param>
    /// <param name="automationName">The descriptive name exposed to UI Automation.</param>
    public SoftKeyboardInputModeButton(string id, object content, string automationName)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("A button presentation identifier is required.", nameof(id));

        Content = content ?? throw new ArgumentNullException(nameof(content));

        AutomationName = !string.IsNullOrWhiteSpace(automationName)
            ? automationName
            : throw new ArgumentException("A button automation name is required.", nameof(automationName));
    }

    /// <summary>
    /// Gets the stable identity used to detect presentation changes between observations.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the reusable content displayed on the mode command key.
    /// </summary>
    public object Content { get; }

    /// <summary>
    /// Gets the descriptive name exposed to UI Automation.
    /// </summary>
    public string AutomationName { get; }

    /// <inheritdoc />
    public bool Equals(SoftKeyboardInputModeButton? other)
    {
        return other is not null
            && Id == other.Id
            && AutomationName == other.AutomationName;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as SoftKeyboardInputModeButton);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Id) * 397)
                ^ StringComparer.Ordinal.GetHashCode(AutomationName);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return AutomationName;
    }
}
