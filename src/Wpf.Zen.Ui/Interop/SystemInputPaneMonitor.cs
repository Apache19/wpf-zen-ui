// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Wpf.Zen.Ui.Interop;

/// <summary>
/// Owns one HWND's input-pane subscription and reports physical screen rectangles on its
/// dispatcher.
/// </summary>
/// <remarks>
/// Location queries cover an already open keyboard and movement of a floating keyboard, for which
/// showing/hiding notifications alone are insufficient. The owner disposes this monitor while its
/// presentation is hidden or unloaded. Native failures leave ordinary WPF layout available.
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class SystemInputPaneMonitor : IFrameworkInputPaneHandler, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<Rect> _changed;
    private readonly DispatcherTimer _timer;
    private IFrameworkInputPane? _pane;
    private uint _cookie;
    private bool _advised;
    private bool _disposed;
    private bool _queryFailed;
    private Rect _lastBounds = Rect.Empty;

    private SystemInputPaneMonitor(HwndSource source, Action<Rect> changed)
    {
        _dispatcher = source.Dispatcher;
        _changed    = changed;

        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        _timer.Tick += OnTick;
    }

    /// <summary>
    /// Acquires the OS service, installs notifications, and reads the initial keyboard position.
    /// A missing service returns null; a failed event subscription still permits location queries.
    /// </summary>
    internal static IDisposable? Subscribe(HwndSource source, Action<Rect> changed)
    {
#if !NETFRAMEWORK
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return null;
        }
#endif
        var monitor = new SystemInputPaneMonitor(source, changed);
        try
        {
            monitor._pane = (IFrameworkInputPane)new FrameworkInputPane();
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            Debug.WriteLine(
                $"[TouchKeyboard.Monitor] Service unavailable; error=0x{exception.HResult:X8}."
            );

            monitor.Dispose();
            return null;
        }

        try
        {
            monitor._pane.AdviseWithHWND(new HWND(source.Handle), monitor, out monitor._cookie);
            monitor._advised = true;
        }
        catch (COMException exception)
        {
            Debug.WriteLine(
                $"[TouchKeyboard.Monitor] Notifications unavailable; error=0x{exception.HResult:X8}."
            );
        }

        monitor.Refresh();
        monitor._timer.Start();

        return monitor;
    }

    /// <summary>
    /// Copies native callback data before returning and dispatches layout work out of the callback.
    /// </summary>
    unsafe void IFrameworkInputPaneHandler.Showing(RECT* rectangle, BOOL ensureFocusedElementInView)
    {
        var bounds = rectangle == null ? Rect.Empty : ToRect(*rectangle);
        QueueBounds(bounds);
    }

    void IFrameworkInputPaneHandler.Hiding(BOOL ensureFocusedElementInView)
    {
        QueueBounds(Rect.Empty);
    }

    private void QueueBounds(Rect bounds)
    {
        if (!_dispatcher.HasShutdownStarted)
        {
            _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() => Publish(bounds))
            );
        }
    }

    private void OnTick(object? sender, EventArgs args)
    {
        Refresh();
    }

    /// <summary>
    /// Polls only while the owning presentation is active and logs failures once until recovery.
    /// </summary>
    private void Refresh()
    {
        if (_disposed || _pane is null)
        {
            return;
        }

        try
        {
            _pane.Location(out var rectangle);
            _queryFailed = false;

            Publish(ToRect(rectangle));
        }
        catch (COMException exception)
        {
            if (!_queryFailed)
            {
                Debug.WriteLine(
                    $"[TouchKeyboard.Monitor] LOCATION query failed; error=0x{exception.HResult:X8}."
                );

                _queryFailed = true;
            }
        }
    }

    private void Publish(Rect bounds)
    {
        if (_disposed || bounds == _lastBounds)
        {
            return;
        }

        _lastBounds = bounds;

        Debug.WriteLine(
            $"[TouchKeyboard.Monitor] OCCLUSION changed; screenBounds={bounds}."
        );

        _changed(bounds);
    }

    private static Rect ToRect(RECT rectangle)
    {
        if (rectangle.right <= rectangle.left || rectangle.bottom <= rectangle.top)
        {
            return Rect.Empty;
        }

        return new Rect(
            rectangle.left,
            rectangle.top,
            rectangle.right - rectangle.left,
            rectangle.bottom - rectangle.top
        );
    }

    /// <summary>
    /// Stops polling, rejects queued callbacks, and releases this monitor's native subscription.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _timer.Stop();
        _timer.Tick -= OnTick;

        if (_pane is not null)
        {
            try
            {
                if (_advised
#if !NETFRAMEWORK
                    && OperatingSystem.IsWindowsVersionAtLeast(10)
#endif
                   )
                {
                    _pane.Unadvise(_cookie);
                }
            }
            catch (COMException exception)
            {
                Debug.WriteLine(
                    $"[TouchKeyboard.Monitor] Unsubscribe failed; error=0x{exception.HResult:X8}."
                );
            }
            finally
            {
                Marshal.ReleaseComObject(_pane);
                _pane = null;
            }
        }
    }
}