// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Creates one candidate session for a loaded soft keyboard.
/// </summary>
public interface ISoftKeyboardCandidateSource
{
    /// <summary>
    /// Creates a session that publishes snapshots through the supplied expiring host.
    /// </summary>
    /// <param name="host">The core-owned candidate host.</param>
    /// <returns>A session owned and disposed by the keyboard.</returns>
    ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host);
}
