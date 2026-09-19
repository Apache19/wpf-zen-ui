// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Uses Windows TSF profiles with WPF input-language fallback as the default input-method source.
/// </summary>
public sealed class WindowsSoftKeyboardInputMethodSource : ISoftKeyboardInputMethodSource
{
    private readonly ISoftKeyboardInputMethodBackend _backend;

    /// <summary>
    /// Gets whether the source belongs to the Dispatcher-wide native notification stream.
    /// </summary>
    internal bool UsesNativeNotifications => _backend is TsfInputMethodService;

    /// <summary>
    /// Gets the optional state sampler belonging to this source's backend.
    /// Application and test backends without this capability never read ambient native state.
    /// </summary>
    internal ISoftKeyboardInputStateBackend? StateBackend => _backend as ISoftKeyboardInputStateBackend;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsSoftKeyboardInputMethodSource"/> class.
    /// </summary>
    public WindowsSoftKeyboardInputMethodSource()
        : this(new TsfInputMethodService())
    {
    }

    internal WindowsSoftKeyboardInputMethodSource(ISoftKeyboardInputMethodBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <summary>
    /// Gets the shared source backed by the documented Windows profile APIs.
    /// </summary>
    public static WindowsSoftKeyboardInputMethodSource Default { get; } = new();

    /// <inheritdoc />
    public ISoftKeyboardInputMethodSession CreateSession()
    {
        return new Session(_backend);
    }

    private sealed class Session(ISoftKeyboardInputMethodBackend backend) :
        ISoftKeyboardInputMethodSession,
        ISoftKeyboardInputModeButtonSession
    {
        private bool _disposed;

        /// <inheritdoc />
        public SoftKeyboardInputMethodSnapshot GetSnapshot()
        {
            ThrowIfDisposed();

            var destination = new ObservableCollection<SoftKeyboardInputMethod>();
            backend.Refresh(destination, out var active);

            return new SoftKeyboardInputMethodSnapshot(destination, active);
        }

        /// <inheritdoc />
        public bool TryActivate(SoftKeyboardInputMethod inputMethod)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend.TryActivate(inputMethod);
        }

        /// <inheritdoc />
        public SoftKeyboardInputModeButton? GetInputModeButton(
            SoftKeyboardInputMethod inputMethod)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend is ISoftKeyboardInputModeButtonBackend buttonBackend
                ? buttonBackend.GetInputModeButton(inputMethod)
                : null;
        }

        /// <inheritdoc />
        public SoftKeyboardInputModeCommandResult TryInvokeInputModeButton(
            SoftKeyboardInputMethod inputMethod, Rect screenBounds)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend is ISoftKeyboardInputModeButtonBackend buttonBackend
                ? buttonBackend.TryInvokeInputModeButton(inputMethod, screenBounds)
                : SoftKeyboardInputModeCommandResult.Unavailable;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Session));
            }
        }
    }
}
