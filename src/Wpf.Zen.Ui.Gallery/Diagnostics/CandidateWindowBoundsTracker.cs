// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Wpf.Zen.Ui.Gallery.Diagnostics;

/// <summary>
/// Tracks the top-level native window below the mouse pointer and outlines its actual Win32 bounds.
/// </summary>
/// <remarks>
/// The tracker is intended for diagnosing IME candidate windows whose transparent margins can still
/// participate in native hit testing. It deliberately reads <c>GetWindowRect</c> rather than WPF
/// layout bounds, because the intercepted mouse input belongs to the native HWND.
/// </remarks>
internal sealed class CandidateWindowBoundsTracker : IDisposable
{
    private const int GwlExStyle      = -20;
    private const int SwpNoActivate   = 0x0010;
    private const int SwpShowWindow   = 0x0040;
    private const int WsExNoActivate  = 0x08000000;
    private const int WsExToolWindow  = 0x00000080;
    private const int WsExTransparent = 0x00000020;

    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly DispatcherTimer _timer;
    private readonly Window _overlay;
    private readonly TextBlock _label;
    private IntPtr _overlayHandle;
    private bool _disposed;

    /// <summary>
    /// Initializes a tracker that samples the native window below the pointer twenty times per second.
    /// </summary>
    internal CandidateWindowBoundsTracker()
    {
        _label = new TextBlock
        {
            Padding = new Thickness(5, 2, 5, 3),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xD8, 0xC4, 0x2B, 0x1C)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _overlay = new Window
        {
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Content = new Border
            {
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(3),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = _label,
            },
        };
        _overlay.SourceInitialized += OnOverlaySourceInitialized;

        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Raised after each successful sample with a human-readable HWND description and pixel bounds.
    /// </summary>
    internal event EventHandler<string>? SnapshotChanged;

    /// <summary>
    /// Starts sampling and shows the outline when a window is found below the mouse pointer.
    /// </summary>
    internal void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CandidateWindowBoundsTracker));
        }

        _timer.Start();
        UpdateOverlay();
    }

    /// <summary>
    /// Stops sampling and removes the diagnostic outline.
    /// </summary>
    internal void Stop()
    {
        _timer.Stop();
        _overlay.Hide();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _overlay.SourceInitialized -= OnOverlaySourceInitialized;
        _overlay.Close();
    }

    /// <summary>
    /// Applies styles that keep the diagnostic HWND out of activation and native mouse hit testing.
    /// </summary>
    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        _overlayHandle = new WindowInteropHelper(_overlay).Handle;
        var styles = NativeMethods.GetWindowLong(_overlayHandle, GwlExStyle);
        _ = NativeMethods.SetWindowLong(
            _overlayHandle,
            GwlExStyle,
            styles | WsExNoActivate | WsExToolWindow | WsExTransparent
        );
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateOverlay();
    }

    /// <summary>
    /// Finds the first visible top-level HWND at the pointer in native Z order and outlines its rectangle.
    /// </summary>
    private void UpdateOverlay()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            _overlay.Hide();
            return;
        }

        IntPtr target = IntPtr.Zero;
        Rect targetBounds = default;

        _ = NativeMethods.EnumWindows(
            (window, _) =>
            {
                if (window == _overlayHandle || !NativeMethods.IsWindowVisible(window)
                    || (NativeMethods.GetWindowLong(window, GwlExStyle) & WsExTransparent) != 0
                    || !NativeMethods.GetWindowRect(window, out var bounds) || !bounds.Contains(point))
                {
                    return true;
                }

                target       = window;
                targetBounds = bounds;
                return false;
            },
            IntPtr.Zero
        );

        if (target == IntPtr.Zero || targetBounds.Width <= 0 || targetBounds.Height <= 0)
        {
            _overlay.Hide();
            return;
        }

        var description = DescribeWindow(target, targetBounds);
        _label.Text = description;

        if (!_overlay.IsVisible)
        {
            _overlay.Show();
        }

        _ = NativeMethods.SetWindowPos(
            _overlayHandle,
            HwndTopmost,
            targetBounds.Left,
            targetBounds.Top,
            targetBounds.Width,
            targetBounds.Height,
            SwpNoActivate | SwpShowWindow
        );
        SnapshotChanged?.Invoke(this, description);
    }

    /// <summary>
    /// Builds the diagnostic label without retaining a process handle between samples.
    /// </summary>
    private static string DescribeWindow(IntPtr window, Rect bounds)
    {
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);

        string processName;
        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            processName = process.ProcessName;
        }
        catch (Exception)
        {
            processName = $"PID {processId}";
        }

        var className = new StringBuilder(256);
        _ = NativeMethods.GetClassName(window, className, className.Capacity);

        return $"{processName} | {className} | HWND 0x{window.ToInt64():X} | "
            + $"({bounds.Left}, {bounds.Top}) {bounds.Width}×{bounds.Height} px";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;

        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int Left;

        internal int Top;

        internal int Right;

        internal int Bottom;

        internal int Width => Right - Left;

        internal int Height => Bottom - Top;

        internal bool Contains(Point point)
        {
            return point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
        }
    }

    /// <summary>
    /// Contains the User32 entry points used by the native-window diagnostic.
    /// </summary>
    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(
            IntPtr window,
            StringBuilder className,
            int maximumCount
        );

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr window, out Rect bounds);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr window, int index, int newLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            int flags
        );

        internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    }
}
