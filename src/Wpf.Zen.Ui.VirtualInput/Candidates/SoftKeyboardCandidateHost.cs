// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Provides an expiring, target-safe publication and input boundary to a candidate session.
/// </summary>
/// <remarks>
/// A host is valid only while its source remains attached to the keyboard. Late publications are
/// ignored after expiration. Publications made from a worker thread are marshalled to the keyboard dispatcher.
/// </remarks>
public sealed class SoftKeyboardCandidateHost
{
    private readonly AsyncLocal<RefreshScope?> _refresh = new();

    private SoftKeyboard? _keyboard;

    internal SoftKeyboardCandidateHost(SoftKeyboard keyboard)
    {
        _keyboard = keyboard;
    }

    /// <summary>
    /// Gets the dispatcher that owns the keyboard and its input target.
    /// </summary>
    public Dispatcher Dispatcher
    {
        get => GetKeyboard().Dispatcher;
    }

    /// <summary>
    /// Gets the active input-method profile, or null when Windows exposes none.
    /// </summary>
    public SoftKeyboardInputMethod? InputMethod
    {
        get => GetKeyboard().ActiveInputMethod;
    }

    /// <summary>
    /// Gets a value indicating whether the keyboard currently owns a visible, editable input target.
    /// </summary>
    public bool CanPresentCandidates
    {
        get => GetKeyboard().CanPresentCandidates;
    }

    /// <summary>
    /// Publishes a new immutable candidate snapshot.
    /// </summary>
    /// <param name="snapshot">The source snapshot to display.</param>
    public void Publish(SoftKeyboardCandidateSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var keyboard = _keyboard;
        if (keyboard is null)
        {
            return;
        }

        var refresh = _refresh.Value;
        keyboard.PublishCandidateSnapshot(this, snapshot, refresh?.Version, refresh?.Token ?? default);
    }

    /// <summary>
    /// Sends a virtual key through the keyboard's current validated input target.
    /// </summary>
    /// <param name="key">The main virtual key.</param>
    /// <param name="modifiers">Modifier keys pressed for this operation.</param>
    /// <returns>Whether Windows accepted the complete key chord.</returns>
    public bool TrySendKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        return GetKeyboard().SendCandidateNavigationKey(key, modifiers);
    }

    /// <summary>
    /// Waits until the active candidate snapshot changes from the supplied version.
    /// </summary>
    /// <param name="version">The version observed before starting an operation.</param>
    /// <param name="timeout">The maximum acknowledgement interval.</param>
    /// <param name="cancellationToken">Cancels a request that no longer owns the input session.</param>
    /// <returns>True when a newer snapshot was published; otherwise false.</returns>
    public Task<bool> WaitForSnapshotChangeAsync(
        int version,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return GetKeyboard().WaitForCandidateSnapshotChangeAsync(version, timeout, cancellationToken);
    }

    /// <summary>
    /// Gets the monotonically increasing version of the displayed candidate snapshot.
    /// </summary>
    public int SnapshotVersion
    {
        get => GetKeyboard().CandidateSnapshotVersion;
    }

    internal SoftKeyboard Keyboard
    {
        get => GetKeyboard();
    }

    internal void Expire()
    {
        _keyboard = null;
    }

    /// <summary>
    /// Captures refresh ownership across awaits and worker-thread publication.
    /// </summary>
    internal IDisposable EnterRefresh(int refreshVersion, CancellationToken cancellationToken)
    {
        var scope = new RefreshScope(this, _refresh.Value, refreshVersion, cancellationToken);
        _refresh.Value = scope;

        return scope;
    }

    private SoftKeyboard GetKeyboard()
    {
        var keyboard = _keyboard ?? throw new InvalidOperationException(
            "The candidate host no longer belongs to an active source session."
        );

        return keyboard;
    }

    private sealed class RefreshScope : IDisposable
    {
        private readonly RefreshScope?              _previous;
        private          SoftKeyboardCandidateHost? _host;

        internal RefreshScope(
            SoftKeyboardCandidateHost host, RefreshScope? previous, int version, CancellationToken token)
        {
            _host     = host;
            _previous = previous;
            Version   = version;
            Token     = token;
        }

        internal int Version { get; }

        internal CancellationToken Token { get; }

        public void Dispose()
        {
            SoftKeyboardCandidateHost? host = _host;

            if (host is null)
            {
                return;
            }

            _host = null;

            host._refresh.Value = _previous;
        }
    }
}