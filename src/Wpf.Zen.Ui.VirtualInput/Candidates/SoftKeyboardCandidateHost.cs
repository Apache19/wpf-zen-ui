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
/// ignored after expiration. Worker-thread publications are marshalled to the keyboard dispatcher.
/// Calls made during refresh, commit or paging inherit that operation's ownership across awaits.
/// Source notifications may publish outside an operation while this host remains active.
/// </remarks>
public sealed class SoftKeyboardCandidateHost
{
    private readonly AsyncLocal<OperationScope?> _operation = new();

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
    /// Gets whether the keyboard currently owns a visible, editable input target.
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

        var operation = _operation.Value;
        if (operation is not null && !operation.Context.IsExecuting)
        {
            return;
        }

        keyboard.PublishCandidateSnapshot(this, snapshot, operation?.Version, operation?.Context);
    }

    /// <summary>
    /// Sends a virtual key through the keyboard's current validated input target.
    /// </summary>
    /// <param name="key">The main virtual key.</param>
    /// <param name="modifiers">Modifier keys pressed for this operation.</param>
    /// <returns>Whether Windows accepted the complete key chord.</returns>
    public bool TrySendKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        return TrySendKey(key, modifiers, CaptureOperationValidity(forInput: true));
    }

    /// <summary>
    /// Checks captured ownership again after focus callbacks and candidate discovery finish.
    /// </summary>
    internal bool TrySendKey(Key key, ModifierKeys modifiers, Func<bool> isCurrent)
    {
        var keyboard = GetKeyboard();
        keyboard.VerifyAccess();
        return keyboard.SendCandidateNavigationKey(key, modifiers, isCurrent);
    }

    /// <summary>
    /// Captures operation ownership for contexts retained outside their original execution flow.
    /// </summary>
    internal Func<bool> CaptureOperationValidity(bool forInput = false)
    {
        var keyboard = GetKeyboard();
        keyboard.VerifyAccess();

        var isInputCurrent = keyboard.CaptureInputLifetime();
        var context        = _operation.Value?.Context;

        return () => ReferenceEquals(keyboard.ActiveCandidateHost, this) && isInputCurrent()
            && (context is null || (forInput ? context.CanSendInput : context.IsCurrent));
    }

    /// <summary>
    /// Waits until the active candidate snapshot changes from the supplied version.
    /// </summary>
    /// <param name="version">The version observed before starting an operation.</param>
    /// <param name="timeout">The maximum acknowledgement interval.</param>
    /// <param name="cancellationToken">Cancels a request that no longer owns input.</param>
    /// <returns>True when a newer snapshot was published; otherwise false.</returns>
    public async Task<bool> WaitForSnapshotChangeAsync(
        int version,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var isCurrent = CaptureOperationValidity();

        cancellationToken.ThrowIfCancellationRequested();

        if (!isCurrent())
        {
            return false;
        }

        var changed = await GetKeyboard()
            .WaitForCandidateSnapshotChangeAsync(version, timeout, cancellationToken)
            .ConfigureAwait(true);

        return changed && isCurrent();
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
    /// Captures compound operation ownership across awaits and worker-thread publication.
    /// </summary>
    internal IDisposable EnterOperation(SoftKeyboardOperationContext context, int? refreshVersion = null)
    {
        var scope = new OperationScope(this, _operation.Value, refreshVersion, context);
        _operation.Value = scope;

        return scope;
    }

    private SoftKeyboard GetKeyboard()
    {
        var keyboard = _keyboard ?? throw new InvalidOperationException(
            "The candidate host no longer belongs to an active source session."
        );

        return keyboard;
    }

    /// <summary>
    /// Restores an enclosing operation while keeping asynchronous flows tied to their owner.
    /// </summary>
    private sealed class OperationScope : IDisposable
    {
        private readonly OperationScope?            _previous;
        private          SoftKeyboardCandidateHost? _host;

        internal OperationScope(
            SoftKeyboardCandidateHost host, OperationScope? previous,
            int? version, SoftKeyboardOperationContext context)
        {
            _host     = host;
            _previous = previous;
            Version   = version;
            Context   = context;
        }

        internal int? Version { get; }

        internal SoftKeyboardOperationContext Context { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            SoftKeyboardCandidateHost? host = _host;

            if (host is null)
            {
                return;
            }

            _host = null;

            host._operation.Value = _previous;
        }
    }
}
