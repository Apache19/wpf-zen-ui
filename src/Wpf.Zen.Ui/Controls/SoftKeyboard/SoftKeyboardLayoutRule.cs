// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Adapts a context predicate and a layout factory into a small, composable provider.
/// </summary>
/// <remarks>
/// Use this for one page, input scope, region or TSF profile. A predicate mismatch never invokes the
/// factory. A matching factory may also decline by returning null. Delegates should derive results
/// from the supplied snapshot and must not retain editors or mutate keyboard state during rebuilding.
/// </remarks>
public sealed class SoftKeyboardLayoutRule : ISoftKeyboardLayoutProvider
{
    private readonly Func<SoftKeyboardLayoutContext, bool> _matches;
    private readonly Func<SoftKeyboardLayoutContext, SoftKeyboardLayout?> _create;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardLayoutRule"/> class.
    /// </summary>
    /// <param name="matches">
    /// Determines whether this rule understands the context.
    /// </param>
    /// <param name="create">
    /// Creates the matching surface or returns null to continue resolution.
    /// </param>
    public SoftKeyboardLayoutRule(
        Func<SoftKeyboardLayoutContext, bool> matches,
        Func<SoftKeyboardLayoutContext, SoftKeyboardLayout?> create)
    {
        _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        _create  = create ?? throw new ArgumentNullException(nameof(create));
    }

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return _matches(context) ? _create(context) : null;
    }
}
