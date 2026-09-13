// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Observes dispatcher-wide WPF and TSF input-method changes independently of candidate UI.
/// </summary>
/// <remarks>
/// Native callbacks only invalidate state. Consumers read a complete snapshot after the callback
/// has unwound and use <see cref="Version"/> to reject observations crossed by another change.
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class InputMethodMonitor
    : ISoftKeyboardTsfActiveLanguageProfileNotifySink, IDisposable
{
    private static readonly Guid ActiveLanguageProfileSinkId = new("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57");

    private readonly Dispatcher _dispatcher;
    private readonly Action     _invalidated;

    private InputMethod? _inputMethod;
    private ITfSource?   _source;
    private IntPtr       _sinkPointer;
    private uint         _cookie;
    private int          _version;
    private bool         _disposed;

    internal InputMethodMonitor(Dispatcher dispatcher, Action invalidated)
    {
        _dispatcher  = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _invalidated = invalidated ?? throw new ArgumentNullException(nameof(invalidated));

        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;

        _inputMethod = InputMethod.Current;
        if (_inputMethod is not null)
        {
            _inputMethod.StateChanged += OnInputMethodStateChanged;
        }

        _dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        EnsureProfileNotifications();
    }

    /// <summary>
    /// Gets the version advanced by every relevant native notification.
    /// </summary>
    internal int Version => _version;

    /// <summary>
    /// Gets whether WPF compartment notifications and the TSF profile sink are both connected.
    /// </summary>
    internal bool HasReliableNotifications => _inputMethod is not null && _source is not null;

    /// <summary>
    /// Retries the TSF profile subscription after WPF has created its text-services host.
    /// </summary>
    internal void EnsureProfileNotifications()
    {
        _dispatcher.VerifyAccess();

        if (_disposed || _source is not null)
        {
            return;
        }

        var source = WpfTextServicesAccessor.TryGetThreadManager() as ITfSource;
        if (source is null)
        {
            return;
        }

        var sinkPointer = IntPtr.Zero;
        try
        {
            sinkPointer = Marshal.GetComInterfaceForObject(
                this,
                typeof(ISoftKeyboardTsfActiveLanguageProfileNotifySink)
            );

            var sinkId = ActiveLanguageProfileSinkId;
            if (source.AdviseSink(ref sinkId, sinkPointer, out var cookie) < 0)
            {
                return;
            }

            _source      = source;
            _sinkPointer = sinkPointer;
            _cookie      = cookie;
            sinkPointer  = IntPtr.Zero;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // A later focus or health check retries after WPF initializes TSF for an editor.
        }
        finally
        {
            if (sinkPointer != IntPtr.Zero)
            {
                _ = Marshal.Release(sinkPointer);
            }
        }
    }

    int ISoftKeyboardTsfActiveLanguageProfileNotifySink.OnActivated(
        ref Guid classId,
        ref Guid profileId,
        int activated)
    {
        Invalidate();
        return 0;
    }

    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs eventArgs)
    {
        Invalidate();
    }

    private void OnInputMethodStateChanged(object sender, InputMethodStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsImeStateChanged || eventArgs.IsImeConversionModeChanged)
        {
            Invalidate();
        }
    }

    private void Invalidate()
    {
        unchecked
        {
            _version++;
        }

        _invalidated();
    }

    private void DisconnectProfileNotifications()
    {
        if (_source is not null && _cookie != 0)
        {
            try
            {
                _ = _source.UnadviseSink(_cookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // WPF can release its TSF host before Dispatcher shutdown reaches this monitor.
            }
        }

        _cookie = 0;
        _source = null;

        if (_sinkPointer != IntPtr.Zero)
        {
            _            = Marshal.Release(_sinkPointer);
            _sinkPointer = IntPtr.Zero;
        }
    }

    private void OnDispatcherShutdownStarted(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    /// <summary>
    /// Releases all WPF and TSF subscriptions. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        InputLanguageManager.Current.InputLanguageChanged -= OnInputLanguageChanged;

        if (_inputMethod is not null)
        {
            _inputMethod.StateChanged -= OnInputMethodStateChanged;
            _inputMethod              =  null;
        }

        DisconnectProfileNotifications();
        _dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or PlatformNotSupportedException
            or TypeLoadException;
    }
}