// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Interop;
using Wpf.Zen.Ui.Gallery.Diagnostics;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;

namespace Wpf.Zen.Ui.Gallery.Views.Windows;

/// <summary>
/// Hosts manual experiments for native IME window messages and dispatcher-wide TSF events.
/// </summary>
public sealed partial class SandboxWindow : IDisposable
{
    private const int MaximumRecordCount = 2000;

    private ImeWindowMessageMonitor? _messageMonitor;
    private TsfEventMonitor? _tsfMonitor;
    private bool _disposed;

    /// <summary>
    /// Initializes the sandbox window and its input-method diagnostic surface.
    /// </summary>
    /// <param name="viewModel">The view model created by the gallery host.</param>
    public SandboxWindow(SandboxWindowViewModel viewModel)
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        Loaded += OnLoaded;
        ImeProbeTextBox.GotKeyboardFocus += OnProbeGotKeyboardFocus;
    }

    /// <summary>
    /// Gets the native-message records displayed by the diagnostic log.
    /// </summary>
    public ObservableCollection<string> MessageRecords { get; } = [];

    /// <summary>
    /// Gets the TSF event records, retained independently from the Windows message log.
    /// </summary>
    public ObservableCollection<string> TsfRecords { get; } = [];

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SourceInitialized -= OnSourceInitialized;
        Closed -= OnClosed;
        Loaded -= OnLoaded;
        ImeProbeTextBox.GotKeyboardFocus -= OnProbeGotKeyboardFocus;
        DisposeMessageMonitor();
        if (_tsfMonitor is not null)
        {
            _tsfMonitor.RecordCaptured -= OnTsfRecordCaptured;
            _tsfMonitor.Dispose();
            _tsfMonitor = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Attaches the Windows message observer once the native window handle exists.
    /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        _messageMonitor = new ImeWindowMessageMonitor(source);
        _messageMonitor.RecordCaptured += OnRecordCaptured;
        _messageMonitor.CaptureSnapshot();
    }

    /// <summary>
    /// Initializes TSF observation and focuses the shared probe editor for both diagnostic pages.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_tsfMonitor is null)
        {
            _tsfMonitor = new TsfEventMonitor(Dispatcher);
            _tsfMonitor.RecordCaptured += OnTsfRecordCaptured;
            _tsfMonitor.IsEnabled = TsfCaptureToggle.IsChecked == true;
        }

        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
        _tsfMonitor.CaptureSnapshot();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    /// <summary>
    /// Retries TSF attachment after WPF finishes creating or focusing the editor's text store.
    /// </summary>
    private void OnProbeGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() => _tsfMonitor?.EnsureConnected()));
    }

    /// <summary>
    /// Keeps TSF history bounded and applies this page's independent scrolling preference.
    /// </summary>
    private void OnTsfRecordCaptured(object? sender, string record)
    {
        TsfRecords.Add(record);
        while (TsfRecords.Count > MaximumRecordCount)
        {
            TsfRecords.RemoveAt(0);
        }

        if (TsfAutoScrollToggle.IsChecked == true)
        {
            TsfLogList.ScrollIntoView(record);
        }
    }

    private void OnTsfCaptureToggleClick(object sender, RoutedEventArgs e)
    {
        if (_tsfMonitor is not null)
        {
            _tsfMonitor.IsEnabled = TsfCaptureToggle.IsChecked == true;
            _tsfMonitor.EnsureConnected();
        }
    }

    private void OnTsfCaptureSnapshotClick(object sender, RoutedEventArgs e)
    {
        _tsfMonitor?.CaptureSnapshot();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    private void OnTsfCopyLogClick(object sender, RoutedEventArgs e)
    {
        if (TsfRecords.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, TsfRecords));
        }
    }

    private void OnTsfClearLogClick(object sender, RoutedEventArgs e)
    {
        _tsfMonitor?.ClearPendingRecords();
        TsfRecords.Clear();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    /// <summary>
    /// Retains a bounded Windows message history with optional automatic scrolling.
    /// </summary>
    private void OnRecordCaptured(object? sender, string record)
    {
        MessageRecords.Add(record);
        while (MessageRecords.Count > MaximumRecordCount)
        {
            MessageRecords.RemoveAt(0);
        }

        if (AutoScrollToggle.IsChecked == true)
        {
            MessageLogList.ScrollIntoView(record);
        }
    }

    private void OnCaptureToggleClick(object sender, RoutedEventArgs e)
    {
        if (_messageMonitor is not null)
        {
            _messageMonitor.IsEnabled = CaptureToggle.IsChecked == true;
        }
    }

    private void OnCaptureSnapshotClick(object sender, RoutedEventArgs e)
    {
        _messageMonitor?.CaptureSnapshot();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    private void OnCopyLogClick(object sender, RoutedEventArgs e)
    {
        if (MessageRecords.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, MessageRecords));
        }
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e)
    {
        MessageRecords.Clear();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    /// <summary>
    /// Detaches the window hook and cancels outstanding delayed IMM state reads.
    /// </summary>
    private void DisposeMessageMonitor()
    {
        if (_messageMonitor is null)
        {
            return;
        }

        _messageMonitor.RecordCaptured -= OnRecordCaptured;
        _messageMonitor.Dispose();
        _messageMonitor = null;
    }
}
