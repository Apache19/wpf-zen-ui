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

        public SoftKeyboardInputMethodSnapshot GetSnapshot()
        {
            ThrowIfDisposed();

            var destination = new ObservableCollection<SoftKeyboardInputMethod>();
            backend.Refresh(destination, out var active);

            return new SoftKeyboardInputMethodSnapshot(destination, active);
        }

        public bool TryActivate(SoftKeyboardInputMethod inputMethod)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend.TryActivate(inputMethod);
        }

        public SoftKeyboardInputMode? GetInputMode(SoftKeyboardInputMethod inputMethod)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend.GetInputMode(inputMethod);
        }

        public bool TrySwitchInputMode(
            SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputMode currentMode)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            if (currentMode is null)
            {
                throw new ArgumentNullException(nameof(currentMode));
            }

            return backend.TrySwitchInputMode(inputMethod, currentMode);
        }

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

        public bool TryInvokeInputModeButton(SoftKeyboardInputMethod inputMethod)
        {
            ThrowIfDisposed();

            if (inputMethod is null)
            {
                throw new ArgumentNullException(nameof(inputMethod));
            }

            return backend is ISoftKeyboardInputModeButtonBackend buttonBackend
                && buttonBackend.TryInvokeInputModeButton(inputMethod);
        }

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
