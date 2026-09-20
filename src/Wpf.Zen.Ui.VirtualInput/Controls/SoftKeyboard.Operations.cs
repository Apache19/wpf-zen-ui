// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Captures shared input ownership for synchronous callbacks and asynchronous operation steps.
/// </summary>
public partial class SoftKeyboard
{
    /// <summary>
    /// Captures the editor, input lifecycle and profile generation before a compound operation.
    /// Validate on the dispatcher after awaits and callbacks, including focus restoration.
    /// </summary>
    internal Func<bool> CaptureInputLifetime()
    {
        // A target can survive unload/reload or a source replacement. Capture all generations
        // as well as object identity so that returning to the same editor cannot revive old work.
        var inputVersion   = _inputOperationVersion;
        var sessionVersion = _inputMethodSessionVersion;
        var generation     = _inputObservation.Generation;
        var target         = ActiveTarget;

        return () => inputVersion == _inputOperationVersion
            && sessionVersion == _inputMethodSessionVersion
            && generation == _inputObservation.Generation
            && ReferenceEquals(target, ActiveTarget);
    }
}
